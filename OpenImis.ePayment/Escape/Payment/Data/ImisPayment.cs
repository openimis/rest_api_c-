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
            var gepgFile = new GepgFoldersCreating(productSPCode, "GepGReconRequest", content, env);
            gepgFile.putToTargetFolderPayment();

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

        public override async Task<PostReqCNResponse> PostReqControlNumberAsync(string OfficerCode, string PaymentId, string PhoneNumber, decimal ExpectedAmount, List<PaymentDetail> products, string controlNumber = null, bool acknowledge = false, bool error = false)
        {
            GepgUtility gepg = new GepgUtility(_hostingEnvironment,config);
            var bill = gepg.CreateBill(Configuration, OfficerCode, PhoneNumber, PaymentId, Math.Round(ExpectedAmount,2), InsureeProducts);
            var signature = gepg.GenerateSignature(bill);

            var signedMesg = gepg.FinaliseSignedMsg(signature);
            var billAck = await gepg.SendHttpRequest("/api/bill/sigqrequest", signedMesg, gepg.GetAccountCodeByProductCode(InsureeProducts.FirstOrDefault().ProductCode), "default.sp.in");

            string mydocpath = System.IO.Path.Combine(env.WebRootPath, "controlNumberAck");
            string namepart = new Random().Next(100000, 999999).ToString();

            string reconc = JsonConvert.SerializeObject(billAck);
            string sentbill = JsonConvert.SerializeObject(bill);

            var content = sentbill + "********************" + reconc;
            var gepgFile = new GepgFoldersCreating(PaymentId, "CN_Request", content, env);
            gepgFile.putToTargetFolderPayment();

            return await base.PostReqControlNumberAsync(OfficerCode, PaymentId, PhoneNumber, ExpectedAmount, products, null, true, false);
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

        public async Task<string> GePGPostCancelPayment(int PaymentId)
        {
            GepgUtility gepg = new GepgUtility(_hostingEnvironment, config);
            
            var GePGCancelPaymentRequest = gepg.CreateGePGCancelPaymentRequest(Configuration, PaymentId);
            string SPCode = gepg.GetAccountCodeByPaymentId(PaymentId);


            var response = await gepg.SendHttpRequest("/api/bill/sigcancel_request", GePGCancelPaymentRequest, SPCode, "changebill.sp.in");



            var content = JsonConvert.SerializeObject(GePGCancelPaymentRequest) + "\n********************\n" + JsonConvert.SerializeObject(response);
            var gepgFile = new GepgFoldersCreating(PaymentId.ToString(), "CancelPayment", content, env);
            gepgFile.putToTargetFolderPayment();

            return response;
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
#endif
    }
}
