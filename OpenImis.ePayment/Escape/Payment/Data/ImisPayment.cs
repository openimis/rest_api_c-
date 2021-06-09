using OpenImis.ePayment.Escape.Payment.Models;
using OpenImis.ePayment.Data;
using OpenImis.ePayment.Extensions;
using OpenImis.ePayment.Models;
using OpenImis.ePayment.Models.Payment;
using OpenImis.ePayment.Models.Payment.Response;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Data.SqlClient;
using System.Data;
using OpenImis.ePayment.Responses;

namespace OpenImis.ePayment.Data
{
    public class ImisPayment : ImisBasePayment
    {
        private IHostingEnvironment env;
        private IConfiguration config;

        public ImisPayment(IConfiguration configuration, IHostingEnvironment hostingEnvironment) : base(configuration, hostingEnvironment)
        {
            env = hostingEnvironment;
            config = configuration;
        }

#if CHF
        public async Task<object> RequestReconciliationReportAsync(int daysAgo, String productSPCode)
        {
            daysAgo = -1 * daysAgo;

            GepgUtility gepg = new GepgUtility(_hostingEnvironment,config);

            ReconcRequest Reconciliation = new ReconcRequest();

            gepgSpReconcReq request = new gepgSpReconcReq();
            request.SpReconcReqId = Math.Abs(Guid.NewGuid().GetHashCode());//Convert.ToInt32(DateTime.UtcNow.Year.ToString() + DateTime.UtcNow.Month.ToString() + DateTime.UtcNow.Day.ToString());
            request.SpCode = productSPCode;
            request.SpSysId = Configuration["PaymentGateWay:GePG:SystemId"];
            request.TnxDt = DateTime.Now.AddDays(daysAgo).ToString("yyyy-MM-dd");
            request.ReconcOpt = 1;

            var requestString = gepg.SerializeClean(request, typeof(gepgSpReconcReq));
            string signature = gepg.GenerateSignature(requestString);
            var signedRequest = gepg.FinaliseSignedMsg(new ReconcRequest() { gepgSpReconcReq = request, gepgSignature = signature }, typeof(ReconcRequest));

            var result = await gepg.SendHttpRequest("/api/reconciliations/sig_sp_qrequest", signedRequest, productSPCode, "default.sp.in");

            var content = signedRequest + "********************" + result;
            GepgFileLogger.Log(productSPCode + "_GepGReconRequest", content, env);
            
            return new { reconcId = request.SpReconcReqId, resp = result };

        }

        public override decimal determineTransferFee(decimal expectedAmount, TypeOfPayment typeOfPayment)
        {
            if (typeOfPayment == TypeOfPayment.BankTransfer || typeOfPayment == TypeOfPayment.Cash) {
                return 0;
            }
            else
            {
                var fee = expectedAmount - (expectedAmount / Convert.ToDecimal(1.011));

                return Math.Round(fee,0);
            }
        }

        public override decimal determineTransferFeeReverse(decimal expectedAmount, TypeOfPayment typeOfPayment)
        {
            if (typeOfPayment == TypeOfPayment.BankTransfer || typeOfPayment == TypeOfPayment.Cash)
            {
                return 0;
            }
            else
            {
                var fee = expectedAmount * Convert.ToDecimal(0.011);
                return Math.Round(fee, 0);
            }
        }

        public override decimal GetToBePaidAmount(decimal ExpectedAmount, decimal TransferFee)
        {
            decimal amount = ExpectedAmount - TransferFee;
            return Math.Round(amount, 0);
        }

        public override async Task<PostReqCNResponse> PostReqControlNumberAsync(string OfficerCode, int PaymentId, string PhoneNumber, decimal ExpectedAmount, List<PaymentDetail> products, string controlNumber = null, bool acknowledge = false, bool error = false, string rejectedReason="")
        {
            GepgUtility gepg = new GepgUtility(_hostingEnvironment,config);

            ExpectedAmount = Math.Round(ExpectedAmount, 2);
            //send request only when we have amount > 0
            if (ExpectedAmount > 0)
            {
                var bill = gepg.CreateBill(Configuration, OfficerCode, PhoneNumber, PaymentId, ExpectedAmount, products);

                if (bill != "-2: error - no policy")
                {
                    var signature = gepg.GenerateSignature(bill);

                    var signedMesg = gepg.FinaliseSignedMsg(signature);
                    var billAck = await gepg.SendHttpRequest("/api/bill/sigqrequest", signedMesg, gepg.GetAccountCodeByProductCode(InsureeProducts.FirstOrDefault().ProductCode), "default.sp.in");

                    string billAckRequest = JsonConvert.SerializeObject(billAck);
                    string sentbill = JsonConvert.SerializeObject(bill);

                    GepgFileLogger.Log(PaymentId, "CN_Request", sentbill + "********************" + billAckRequest, env);

                    //get the error code from ackn GePG request
                    var errorCodes = LoadResponseCodeFromXmlAkn(billAck);
                    if (errorCodes == "7101")
                    {
                        return await base.PostReqControlNumberAsync(OfficerCode, PaymentId, PhoneNumber, ExpectedAmount, products, null, true, false);
                    }
                    else 
                    {
                        //we have an error from GePG ackn - then save rejected reason
                        var rejectedReasonText = PrepareRejectedReason(PaymentId, errorCodes);
                        return await base.PostReqControlNumberAsync(OfficerCode, PaymentId, PhoneNumber, ExpectedAmount, products, null, true, true, rejectedReasonText);
                    }
                }
                else 
                {
                    return await base.PostReqControlNumberAsync(OfficerCode, PaymentId, PhoneNumber, ExpectedAmount, products, null, true, true);
                }
            }
            else
            {
                //do not send any request to GePG when we have 0 or negative amount
                return await base.PostReqControlNumberAsync(OfficerCode, PaymentId, PhoneNumber, ExpectedAmount, products, null, true, true); 
            }
        }

        public string ControlNumberResp(int code)
        {
            GepgUtility gepg = new GepgUtility(_hostingEnvironment,config);

            gepgBillSubRespAck CnAck = new gepgBillSubRespAck();
            CnAck.TrxStsCode = code;

            var CnAckString = gepg.SerializeClean(CnAck, typeof(gepgBillSubRespAck));
            string signature = gepg.GenerateSignature(CnAckString);
            var signedCnAck = gepg.FinaliseSignedAcks(new GepgBillResponseAck() { gepgBillSubRespAck = CnAck, gepgSignature = signature }, typeof(GepgBillResponseAck));

            return signedCnAck;
        }

        public async Task<Object> GePGPostCancelPayment(int PaymentId)
        {
            GepgUtility gepg = new GepgUtility(_hostingEnvironment, config);

            try
            {
                var GePGCancelPaymentRequest = gepg.CreateGePGCancelPaymentRequest(Configuration, PaymentId);
                string SPCode = gepg.GetAccountCodeByPaymentId(PaymentId);


                var response = await gepg.SendHttpRequest("/api/bill/sigcancel_request", GePGCancelPaymentRequest, SPCode, "changebill.sp.in");



                var content = JsonConvert.SerializeObject(GePGCancelPaymentRequest) + "\n********************\n" + JsonConvert.SerializeObject(response);
                GepgFileLogger.Log(PaymentId, "CancelPayment", content, env);

                var errorCodes = LoadResponseCodeFromXmlAkn(response);
                if (errorCodes != "7101")
                {
                    var rejectedReasonText = PrepareRejectedReason(PaymentId, errorCodes);
                    setRejectedReason(PaymentId, rejectedReasonText);
                }

                return this.GetGePGObjectFromString(response, typeof(GePGPaymentCancelResponse));
            }
            catch (Exception ex)
            {
                return new DataMessage
                {
                    Code = -1,
                    ErrorOccured = true,
                    MessageValue = ex.ToString(),
                }; 
            }
        }

        public Object GetGePGObjectFromString(string input, Type type)
        {
            try
            {
                TextReader reader = new StringReader(input);
                var serializer = new XmlSerializer(type);
                return Convert.ChangeType(serializer.Deserialize(reader), type);
            }
            catch
            {
                return null;
            }

        }

        public string PaymentResp(int code)
        {
            GepgUtility gepg = new GepgUtility(_hostingEnvironment,config);

            gepgPmtSpInfoAck PayAck = new gepgPmtSpInfoAck();
            PayAck.TrxStsCode = code;

            var PayAckString = gepg.SerializeClean(PayAck, typeof(gepgPmtSpInfoAck));
            string signature = gepg.GenerateSignature(PayAckString);
            var signedPayAck = gepg.FinaliseSignedAcks(new GepgPaymentAck() { gepgPmtSpInfoAck = PayAck, gepgSignature = signature }, typeof(GepgPaymentAck));

            return signedPayAck;
        }

        public string ReconciliationResp(int code)
        {
            GepgUtility gepg = new GepgUtility(_hostingEnvironment,config);

            gepgSpReconcRespAck ReconcAck = new gepgSpReconcRespAck();
            ReconcAck.ReconcStsCode = code;

            var ReconcAckString = gepg.SerializeClean(ReconcAck, typeof(gepgSpReconcRespAck));
            string signature = gepg.GenerateSignature(ReconcAckString);
            var signedReconcAck = gepg.FinaliseSignedAcks(new GepgReconcRespAck() { gepgSpReconcRespAck = ReconcAck, gepgSignature = signature }, typeof(GepgReconcRespAck));

            return signedReconcAck;
        }

        public List<String> GetProductsSPCode()
        {

            var getProductsSPCodes = @"SELECT DISTINCT tblProduct.AccCodePremiums FROM tblProduct WHERE tblProduct.AccCodePremiums LIKE 'SP[0-9][0-9][0-9]' AND tblProduct.ValidityTo is NULL";

            SqlParameter[] parameters = { };

            try
            {
                DataTable results = dh.GetDataTable(getProductsSPCodes, parameters, CommandType.Text);
                List<String> productsCodes = new List<String>();
                if (results.Rows.Count > 0)
                {
                    foreach (DataRow result in results.Rows)
                    {
                        if (!string.IsNullOrEmpty(Convert.ToString(result["AccCodePremiums"])))
                        {
                            productsCodes.Add(Convert.ToString(result["AccCodePremiums"]));
                        }
                    }
                }

                return productsCodes;
            }
            catch (Exception e)
            {

                throw e;
            }
        }

        public object GetPaymentToReconciliate(ReconcTrxInf payment)
        {
            object result = null;
            double paidAmount = Convert.ToDouble(payment.PaidAmt);
            SqlParameter[] parameters = {
                new SqlParameter("@Id", payment.SpBillId),
                new SqlParameter("@paidAmount", payment.PaidAmt),
                new SqlParameter("@PaymentStatus", PaymentStatus.Reconciliated),
            };

            var sSQL = @"SELECT PaymentId, ExpectedAmount, ReceivedAmount, PaymentStatus FROM tblPayment WHERE PaymentId=@Id And PaymentStatus<=@PaymentStatus And ExpectedAmount=@paidAmount And ValidityTo is Null";

            try
            {
                var data = dh.GetDataTable(sSQL, parameters, CommandType.Text);

                if (data.Rows.Count > 0)
                {
                    for (int i = 0; i < data.Rows.Count; i++)
                    {
                        var rw = data.Rows[i];
                        var expectedAmount = rw["ExpectedAmount"] != System.DBNull.Value ? Convert.ToDouble(rw["ExpectedAmount"]) : 0;
                        var receivedAmount = rw["ReceivedAmount"] != System.DBNull.Value ? Convert.ToDouble(rw["ReceivedAmount"]) : 0;
                        if (paidAmount == expectedAmount && receivedAmount == paidAmount)
                        {
                            result = new
                            {
                                paymentId = rw["PaymentID"].ToString(),
                                expectedAmount = expectedAmount,
                                receivedAmount = receivedAmount,
                                paymentStatus = rw["PaymentStatus"],
                            };
                        }
                    }
                }
            }
            catch (Exception e)
            { }

            return result;
        }

        private string LoadResponseCodeFromXmlAkn(string xmlContent) 
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlContent);

            XmlNodeList errorCodeTag = doc.GetElementsByTagName("TrxStsCode");
            if (errorCodeTag.Count < 1)
            {
                return "";
            }
            else
            {
                string errorCode = errorCodeTag[0].InnerText;
                return errorCode;
            }
        }

        public string PrepareRejectedReason(int billId, string errorCodes = "7101")
        {
            //prepare to save RejectedReason column the error codes and short description of error from GePG
            var rejectedReason = "";
            if (errorCodes != "7101")
            {
                //split error codes
                var listOfErrors = errorCodes.Split(';');
                for (var i = 0; i < listOfErrors.Length; i++)
                {
                    if (i != listOfErrors.Length - 1)
                    {
                        rejectedReason += listOfErrors[i] + ":" + GepgCodeResponses.GepgResponseCodes.FirstOrDefault(x => x.Value == int.Parse(listOfErrors[i])).Key + ";";
                    }
                    else
                    {
                        rejectedReason += listOfErrors[i] + ":" + GepgCodeResponses.GepgResponseCodes.FirstOrDefault(x => x.Value == int.Parse(listOfErrors[i])).Key;
                    }
                }
            }
            return rejectedReason;
        }

        public void insertPaymentBillNotExist(PaymentData model, int paymentStatus)
        {
            var sSQL = @"INSERT INTO [dbo].[tblPayment]
			(PaymentDate, ReceivedDate, ReceivedAmount, ReceiptNo, TransactionNo, PaymentOrigin, PayerPhoneNumber, PaymentStatus, OfficerCode, ValidityFrom, AuditedUSerID, RejectedReason) 
			VALUES (@PaymentDate, @ReceiveDate,  @Amount, @ReceiptNo, @TransactionNo, @PaymentOrigin, @PayerPhoneNumber, @PaymentStatus, @OfficerCode,  GETDATE(), -1, @ErrorMsg)";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentDate", model.payment_date),
                new SqlParameter("@ReceiveDate", model.received_date),
                new SqlParameter("@ControlNumber", model.control_number),
                new SqlParameter("@Amount", model.received_amount),
                new SqlParameter("@ReceiptNo", model.receipt_identification),
                new SqlParameter("@TransactionNo", model.transaction_identification),
                new SqlParameter("@PayerPhoneNumber", model.payer_phone_number),
                new SqlParameter("@PaymentStatus", paymentStatus),
                new SqlParameter("@PaymentOrigin", model.payment_origin),
                new SqlParameter("@OfficerCode", model.enrolment_officer_code),
                new SqlParameter("@ErrorMsg", GepgCodeResponses.GepgResponseCodes["Bill does not exist"].ToString()+":Bill does not exist")
            };

            try
            {
                dh.Execute(sSQL, parameters, CommandType.Text);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public int GetLastInsertedPaymentId()
        {
            var sSQL = @"SELECT TOP(1) PaymentID FROM tblPayment ORDER BY PaymentID DESC";

            SqlParameter[] parameters = {
            };
            try
            {
                var data = dh.GetDataTable(sSQL, parameters, CommandType.Text);
                if (data.Rows.Count > 0)
                {
                    var row = data.Rows[0];
                    return Convert.ToInt32(row["PaymentID"]);
                }
            }
            catch (Exception)
            {
                return 0;
            }
            return 0;
        }

        public void insertPaymentDetailBillNotExist(PaymentData model, int billId)
        {
            var sSQL = @"INSERT INTO [dbo].[tblPaymentDetails]
				(PaymentID, ProductCode, InsuranceNumber, PolicyStage, ValidityFrom, AuditedUserId) 
				VALUES (@PaymentID, NULL, NULL, 'N', GETDATE(), -1)
			";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", billId),
            };

            try
            {
                dh.Execute(sSQL, parameters, CommandType.Text);
            }
            catch (Exception e)
            {

                throw e;
            }
        }


#endif
    }
}
