using OpenImis.ePayment.Escape.Payment.Models;
using OpenImis.ePayment.Data;
using OpenImis.ePayment.Logic;
using OpenImis.ePayment.Models;
using OpenImis.ePayment.Models.Payment;
using OpenImis.ePayment.Models.Payment.Response;
using OpenImis.ePayment.Responses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenImis.ePayment.Data
{
    public class ImisBasePayment
    {
        public string PaymentId { get; set; }
        public decimal ExpectedAmount { get; set; }
        public string ControlNum { get; set; }
        public string PhoneNumber { get; set; }
        public Language Language { get; set; }
        public TypeOfPayment? typeOfPayment { get; set; }

        public DateTime? PaymentDate { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? OutStAmount { get; set; }
        public bool SmsRequired { get; set; }
        public List<InsureeProduct> InsureeProducts { get; set; }

        protected IConfiguration Configuration;
        protected readonly IHostingEnvironment _hostingEnvironment;
        protected DataHelper dh;

        public ImisBasePayment(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
            dh = new DataHelper(configuration);
        }

        public async Task<bool> SaveControlNumberRequest(string BillId,bool failed)
        {

            SqlParameter[] sqlParameters = {
                new SqlParameter("@PaymentID", BillId),
                new SqlParameter("@Failed", failed)
             };

            try
            {
                var data = await dh.ExecProcedureAsync("uspRequestGetControlNumber", sqlParameters);
                GetPaymentInfo(BillId);
            }
            catch (Exception e)
            {
                throw e;
            }

            return true;
        }

        public virtual async Task<PostReqCNResponse> PostReqControlNumberAsync(string OfficerCode, string PaymentId,string PhoneNumber, decimal ExpectedAmount, List<PaymentDetail> products,string controlNumber = null,bool acknowledge = false,bool error = false)
        {
            bool result = await SaveControlNumberRequest(PaymentId,error);
            string ctrlNumber = null;
#if !CHF
            // BEGIN Temporary Control Number Generator(Simulation For Testing Only)
            var randomNumber = new Random().Next(100000, 999999);
            ctrlNumber = randomNumber.ToString();
            //END Temporary 
#endif
            PostReqCNResponse response = new PostReqCNResponse() {
               
                ControlNumber = ctrlNumber,
                Posted = true,
                ErrorCode = 0,
                Assigned = error
            };

            return response;
        }

        public virtual bool UpdatePaymentTransferFee(string paymentId, decimal TransferFee, TypeOfPayment typeOfPayment) {

            var sSQL = @"UPDATE tblPayment SET TypeOfPayment = @TypeOfPayment,TransferFee = @TransferFee WHERE PaymentID = @paymentId";

            SqlParameter[] parameters = {
                new SqlParameter("@paymentId", paymentId),
                new SqlParameter("@TransferFee", TransferFee),
                new SqlParameter("@TypeOfPayment", Enum.GetName(typeof(TypeOfPayment),typeOfPayment))
            };
             
            try
            {
                dh.Execute(sSQL, parameters, CommandType.Text);
               
                return true;
            }
            catch (Exception e)
            {
                return false;
            }

        }

        public virtual decimal determineTransferFee(decimal expectedAmount, TypeOfPayment typeOfPayment)
        {
            return 0;
        }

        public virtual decimal determineTransferFeeReverse(decimal expectedAmount, TypeOfPayment typeOfPayment)
        {
            return 0;
        }

        public virtual int GetReqControlNumberAck(string paymentId)
        {
            return 0;
        }

        public virtual int GetPaymentDataAck(string paymentId, string controlNumber)
        {
            return 0;
        }

        public virtual decimal GetToBePaidAmount(decimal ExpectedAmount, decimal TransferFee)
        {
            decimal amount = ExpectedAmount - TransferFee;
            return Math.Round(amount,2);
        }

        public DataMessage SaveIntent(IntentOfPay _intent, int? errorNumber = 0, string errorMessage = null)
        {
            var Proxyfamily = LocalDefault.FamilyMembers(Configuration);

            List<PaymentDetail> policies = new List<PaymentDetail>();

            if (_intent.policies != null)
            {
                policies = _intent.policies;
            }

            XElement PaymentIntent = new XElement("PaymentIntent",
                    new XElement("Header",
                        new XElement("OfficerCode", _intent.enrolment_officer_code),
                        new XElement("RequestDate",_intent.request_date),
                        new XElement("PhoneNumber", _intent.phone_number),
                        new XElement("LanguageName",_intent.language),
                        new XElement("SmsRequired", Convert.ToInt32(_intent.SmsRequired)),
                        new XElement("AuditUserId", -1)
                    ),
                      new XElement("Details",
                         policies.Select(x =>

                               new XElement("Detail",
                                  new XElement("InsuranceNumber", x.insurance_number),
                                  new XElement("ProductCode", x.insurance_product_code),
                                  new XElement("EnrollmentDate", DateTime.UtcNow),
                                  new XElement("IsRenewal", x.IsRenewal())
                                  )
                    )
                  ),
                   new XElement("ProxySettings",
                        new XElement("AdultMembers", Proxyfamily.Adults),
                        new XElement("ChildMembers", Proxyfamily.Children),
                        new XElement("OAdultMembers", Proxyfamily.OtherAdults),
                        new XElement("OChildMembers", Proxyfamily.OtherChildren)
                   )
            );



            SqlParameter[] sqlParameters = {
                new SqlParameter("@Xml", PaymentIntent.ToString()),
                new SqlParameter("@ErrorNumber",errorNumber),
                new SqlParameter("@ErrorMsg",errorMessage),
                new SqlParameter("@PaymentID", SqlDbType.Int){Direction = ParameterDirection.Output },
                new SqlParameter("@ExpectedAmount", SqlDbType.Decimal){Direction = ParameterDirection.Output },
                new SqlParameter("@ProvidedAmount",_intent.amount_to_be_paid),
                new SqlParameter("@PriorEnrollment",LocalDefault.PriorEnrolmentRequired(Configuration))
             };

            DataMessage message;

            try
            {
                bool error = true;
                var data = dh.ExecProcedure("uspInsertPaymentIntent", sqlParameters);

                var rv = int.Parse(data[2].Value.ToString());

                if (rv == 0)
                { 
                    error = false;
                }

                DataTable dt = new DataTable();
                dt.Clear();
                dt.Columns.Add("internal_identifier");
                dt.Columns.Add("control_number");

                DataRow rw = dt.NewRow();
                var PaymentId = data[0].Value.ToString();
                rw["internal_identifier"] = PaymentId;
                
                dt.Rows.Add(rw);

                ExpectedAmount = decimal.Parse(data[1].Value.ToString());

                var languages = LocalDefault.PrimaryLanguageRepresentations(Configuration);

                if (_intent.language == null || languages.Contains(_intent.language.ToLower()))
                {
                    Language = Language.Primary;
                }
                else
                {
                    Language = Language.Secondary;
                }

                message = new SaveIntentResponse(rv, error,dt,(int)Language).Message;
                GetPaymentInfo(PaymentId);
            }
            catch (Exception e)
            {

                message = new SaveIntentResponse(e).Message;
            }

            return message;
        }

        public DataMessage SaveControlNumber(string ControlNumber,bool failed)
        {
            SqlParameter[] sqlParameters = {
                new SqlParameter("@PaymentID", PaymentId),
                new SqlParameter("@ControlNumber", ControlNumber),
                new SqlParameter("@Failed", failed)
             };

            DataMessage message;

            try
            {
                
                var data = dh.ExecProcedure("uspReceiveControlNumber", sqlParameters);
                message = new CtrlNumberResponse(int.Parse(data[0].Value.ToString()), false, (int)Language).Message;
                GetPaymentInfo(PaymentId);
            }
            catch (Exception e)
            {

                message = new CtrlNumberResponse(e).Message;
            }

            return message;

        }

        public DataMessage SaveControlNumber(ControlNumberResp model,bool failed)
        {
            SqlParameter[] sqlParameters = {
                new SqlParameter("@PaymentID", model.internal_identifier),
                new SqlParameter("@ControlNumber", model.control_number),
                new SqlParameter("@Failed", failed),
                //new SqlParameter("@Message", model.error_message)
             };

            DataMessage message;

            try
            {
                var data = dh.ExecProcedure("uspReceiveControlNumber", sqlParameters);
                message = new CtrlNumberResponse(int.Parse(data[0].Value.ToString()), false, (int)Language).Message;
                GetPaymentInfo(model.internal_identifier);
            }
            catch (Exception e)
            {
                message = new CtrlNumberResponse(e).Message;
            }

            return message;
        }

        public bool CheckControlNumber(string PaymentID, string ControlNumber)
        {
            var sSQL = @"SELECT * FROM tblControlNumber WHERE PaymentID != @PaymentID AND ControlNumber = @ControlNumber";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", PaymentID),
                new SqlParameter("@ControlNumber", ControlNumber)
            };
            bool result = false;

            try
            {
                var data = dh.GetDataTable(sSQL, parameters, CommandType.Text);
                if (data.Rows.Count > 0)
                {
                    result = true;
                }
                //GetPaymentInfo(PaymentID);
            }
            catch (Exception e)
            {
                return false;
            }

            return result;
        }

        public void UpdateControlNumberStatus(string ControlNumber, CnStatus status)
        {

            SqlParameter[] sqlParameters = {
                  new SqlParameter("@ControlNumber", ControlNumber)
            };
             
            switch (status)
            {
                case CnStatus.Sent:
                    break;
                case CnStatus.Acknowledged:
                    dh.ExecProcedure("uspAcknowledgeControlNumber", sqlParameters);
                    break;
                case CnStatus.Issued:
                    dh.ExecProcedure("uspIssueControlNumber", sqlParameters);
                    break;
                case CnStatus.Paid:
                    dh.ExecProcedure("uspPaidControlNumber", sqlParameters);
                    break;
                case CnStatus.Rejected:
                    break;
                default:
                    break;
            }
        }
      
        public DataMessage SaveControlNumberAkn(bool error_occured, string Comment)
        {
            XElement CNAcknowledgement = new XElement("ControlNumberAcknowledge",
                  new XElement("PaymentID", PaymentId),
                  new XElement("Success", Convert.ToInt32(!error_occured)),
                  new XElement("Comment", Comment)
                  );

            SqlParameter[] sqlParameters = {
                new SqlParameter("@Xml",CNAcknowledgement.ToString())
             };


            DataMessage message;

            try
            {
                var data = dh.ExecProcedure("uspAcknowledgeControlNumberRequest", sqlParameters);
                message = new SaveAckResponse(int.Parse(data[0].Value.ToString()), false, (int)Language).Message;
                GetPaymentInfo(PaymentId);
            }
            catch (Exception e)
            {

                message = new SaveAckResponse(e).Message;
            }

            return message;

        }


        public DataMessage SavePayment(PaymentData payment,bool failed = false)
        {
            int? isRenewal = null;

            if((payment.renewal != null))
            {
                isRenewal = (int)payment.renewal;
            }

            var paymentId = GetPaymentId(payment.control_number);

            XElement PaymentIntent = new XElement("PaymentData",
                new XElement("PaymentID", paymentId),
                new XElement("PaymentDate", payment.payment_date),
                new XElement("ReceiveDate", payment.received_date),
                new XElement("ControlNumber", payment.control_number),
                new XElement("Amount", payment.received_amount),
                new XElement("ReceiptNo", payment.receipt_identification),
                new XElement("TransactionNo", payment.transaction_identification),
                new XElement("PhoneNumber", payment.payment_origin),
                new XElement("PaymentOrigin", payment.payment_origin),
                new XElement("OfficerCode", payment.enrolment_officer_code),
                new XElement("LanguageName",payment.language), // not used
                new XElement("Detail",
                    new XElement("InsureeNumber", payment.insurance_number),
                    new XElement("ProductCode", payment.insurance_product_code),
                    new XElement("IsRenewal", isRenewal)
                            )
               );


            SqlParameter[] sqlParameters = {
                new SqlParameter("@Xml", PaymentIntent.ToString()),
                new SqlParameter("@Payment_ID",SqlDbType.BigInt){Direction = ParameterDirection.Output }
             };
            
            DataMessage message;

            try
            {
                var data = dh.ExecProcedure("uspReceivePayment", sqlParameters);
                // TODO: manage error messages from SP execution 
                message = new SavePayResponse(int.Parse(data[1].Value.ToString()), false, (int)Language).Message;
                GetPaymentInfo(data[0].Value.ToString());

            }
            catch (Exception e)
            {
                message = new SavePayResponse(e).Message;
            }

            return message;

        }

        public DataMessage MatchPayment(MatchModel model)
        {
            SqlParameter[] sqlParameters = {
                new SqlParameter("@PaymentID", model.internal_identifier),
                new SqlParameter("@AuditUserId", model.audit_user_id)
             };

            DataMessage message;

            try
            {
                DataSet data = dh.FillDataSet("uspMatchPayment", sqlParameters, CommandType.StoredProcedure);

                //bool error = false;
                DataTable dt = new DataTable();

                if (data.Tables.Count > 0)
                {
                    dt = data.Tables[data.Tables.Count - 1];

                //    error = true;

                //    if (dt.Rows.Count > 0)
                //    {
                //        var firstRow = dt.Rows[0];

                //        if (Convert.ToInt32(firstRow["PaymentMatched"]) > 0)
                //        {
                //            error = false;
                //        }

                //    }
                //    else
                //    {
                //        error = true;
                //    }
                }
                
                message = new MatchPayResponse(dh.ReturnValue,false,dt , (int)Language).Message;
                if(model.internal_identifier != null && !message.ErrorOccured)
                {
                    GetPaymentInfo(model.internal_identifier.ToString());
                }
            }
            catch (Exception e)
            {
                message = new ImisApiResponse(e).Message;
            }

            return message;
        }


        public async Task<DataMessage> GetControlNumbers(string PaymentIds)
        {
            
            var sSQL = String.Format(@"SELECT PaymentID,ControlNumber
                         FROM tblControlNumber WHERE PaymentID IN({0})", PaymentIds);

            SqlParameter[] sqlParameters = {
               
             };

            DataMessage dt = new DataMessage();
            try
            {
                DataTable data = dh.GetDataTable(sSQL, sqlParameters, CommandType.Text);
                data.Columns["PaymentID"].ColumnName = "internal_identifier";
                data.Columns["ControlNumber"].ColumnName = "control_number";

                var ids = PaymentIds.Split(",");

                if (ids.Distinct().Count() == data.Rows.Count)
                {
                    dt = new RequestedCNResponse(0, false, data, (int)Language).Message;
                }
                else
                {
                    var _datastring = JsonConvert.SerializeObject(data);
                    var _data = JsonConvert.DeserializeObject<List<AssignedControlNumber>>(_datastring);
                    var _ids = _data.Select(x => x.internal_identifier).ToArray();

                    DataTable invalid = new DataTable();
                    invalid.Clear();
                    invalid.Columns.Add("internal_identifier");
                    invalid.Columns.Add("control_number");

                    foreach (var id in ids.Except(_ids))
                    {
                        DataRow rw = invalid.NewRow();

                        rw["internal_identifier"] = id;

                        invalid.Rows.Add(rw);
                    }
                   

                    dt = new RequestedCNResponse(2, true, invalid, (int)Language).Message;
                }
                
            }
            catch (Exception e)
            {
                throw e;
            }

            return dt;
        }

        public void GetPaymentInfo(string Id)
        {
            var sSQL = @"SELECT tblPayment.PaymentID, tblPayment.ExpectedAmount,tblPayment.LanguageName,tblPayment.TypeOfPayment,tblPayment.SmsRequired, tblPaymentDetails.ExpectedAmount AS ExpectedDetailAmount,
                        tblPayment.ReceivedAmount, tblPayment.PaymentDate, tblInsuree.LastName, tblInsuree.OtherNames,tblPaymentDetails.InsuranceNumber,tblPayment.PhoneNumber,
                        tblProduct.ProductName, tblPaymentDetails.ProductCode, tblPolicy.ExpiryDate, tblPolicy.EffectiveDate,tblControlNumber.ControlNumber,tblPolicy.PolicyStatus, tblPolicy.PolicyValue - ISNULL(mp.PrPaid,0) Outstanding
                        FROM tblControlNumber 
                        RIGHT OUTER JOIN tblInsuree 
                        RIGHT OUTER JOIN tblProduct 
                        RIGHT OUTER JOIN tblPayment 
                        INNER JOIN tblPaymentDetails 
                        ON tblPayment.PaymentID = tblPaymentDetails.PaymentID 
                        ON tblProduct.ProductCode = tblPaymentDetails.ProductCode 
                        ON tblInsuree.CHFID = tblPaymentDetails.InsuranceNumber 
                        ON tblControlNumber.PaymentID = tblPayment.PaymentID 
                        LEFT OUTER JOIN tblPremium 
                        LEFT OUTER JOIN tblPolicy
						LEFT OUTER JOIN (
						select P.PolicyID PolID, SUM(P.Amount) PrPaid from tblpremium P inner join tblPaymentDetails PD ON PD.PremiumID = P.PremiumId INNER JOIN tblPayment Pay ON Pay.PaymentID = PD.PaymentID  where P.ValidityTo IS NULL AND Pay.PaymentStatus  = 5 GROUP BY P.PolicyID
						) MP ON MP.PolID = tblPolicy.PolicyID
                        ON tblPremium.PolicyID = tblPolicy.PolicyID 
                        ON tblPaymentDetails.PremiumID = tblPremium.PremiumId
                        WHERE (tblPayment.PaymentID = @PaymentID) AND (tblProduct.ValidityTo IS NULL) AND (tblInsuree.ValidityTo IS NULL)";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", Id)
            };

            try
            {
                var data = dh.GetDataTable(sSQL, parameters, CommandType.Text);
               
                if (data.Rows.Count > 0)
                {
                    var row1 = data.Rows[0];
                    PaymentId = Id;
                    ControlNum = row1["ControlNumber"] != System.DBNull.Value ? Convert.ToString(row1["ControlNumber"]):null;
                    ExpectedAmount = row1["ExpectedAmount"] != System.DBNull.Value ? Convert.ToDecimal(row1["ExpectedAmount"]):0;
                    SmsRequired = row1["SmsRequired"] != System.DBNull.Value ? Convert.ToBoolean(row1["SmsRequired"]) : false;

                    var language = row1["LanguageName"] != System.DBNull.Value ? Convert.ToString(row1["LanguageName"]) : "en";
                    var languages = LocalDefault.PrimaryLanguageRepresentations(Configuration);

                    if (language == null || languages.Contains(language.ToLower()))
                    {
                        Language = Language.Primary;
                    }
                    else
                    {
                        Language = Language.Secondary;
                    }
                    typeOfPayment = row1["TypeOfPayment"] != System.DBNull.Value ? (TypeOfPayment?)Enum.Parse(typeof(TypeOfPayment),Convert.ToString(row1["TypeOfPayment"]),true) : null;

                    PhoneNumber = row1["PhoneNumber"] != System.DBNull.Value ? Convert.ToString(row1["PhoneNumber"]):null;
                    PaymentDate = (DateTime?)(row1["PaymentDate"] != System.DBNull.Value ? row1["PaymentDate"] : null);
                    PaidAmount = (decimal?)(row1["ReceivedAmount"] != System.DBNull.Value ? row1["ReceivedAmount"] : null);
                    OutStAmount = (decimal?)(row1["Outstanding"] != System.DBNull.Value ? row1["Outstanding"] : null);
                    InsureeProducts = new List<InsureeProduct>();

                    for (int i = 0; i < data.Rows.Count; i++)
                    {
                        var rw = data.Rows[i];

                        bool active = false;

                        if (rw["PolicyStatus"] != System.DBNull.Value && Convert.ToInt32(rw["PolicyStatus"]) == 2) {
                            active = true;
                        }
                        var othernames = rw["OtherNames"] != System.DBNull.Value ? Convert.ToString(rw["OtherNames"]):null;
                        var lastname = rw["LastName"] != System.DBNull.Value ? Convert.ToString(rw["LastName"]):null;
                        InsureeProducts.Add(
                                new InsureeProduct()
                                {
                                    
                                    InsureeNumber = rw["InsuranceNumber"] != System.DBNull.Value ? Convert.ToString(rw["InsuranceNumber"]):null,
                                    InsureeName = othernames +" "+lastname,
                                    ProductName = rw["ProductName"] != System.DBNull.Value ? Convert.ToString(rw["ProductName"]):null,
                                    ProductCode = rw["ProductCode"] != System.DBNull.Value ? Convert.ToString(rw["ProductCode"]):null,
                                    ExpiryDate = (DateTime?)(rw["ExpiryDate"] != System.DBNull.Value?rw["ExpiryDate"] :null),
                                    EffectiveDate = (DateTime?)(rw["EffectiveDate"] != System.DBNull.Value ? rw["EffectiveDate"] : null),
                                    PolicyActivated = active,
                                    ExpectedProductAmount = rw["ExpectedDetailAmount"] != System.DBNull.Value ? Convert.ToDecimal(rw["ExpectedDetailAmount"]) : 0
                                 }
                            );               
                    }

                }
                else
                {
                    throw new DataException("3-Wrong Control Number");
                }
            }
            catch (Exception e)
            {
                throw e;
            }

        }


        public List<MatchSms> GetPaymentIdsForSms()
        {
            
            var sSQl = @"SELECT tblPayment.PaymentID,tblPayment.DateLastSMS,tblPayment.MatchedDate
                        FROM tblControlNumber 
                        RIGHT OUTER JOIN tblInsuree 
                        RIGHT OUTER JOIN tblProduct 
                        RIGHT OUTER JOIN tblPayment 
                        INNER JOIN tblPaymentDetails 
                        ON tblPayment.PaymentID = tblPaymentDetails.PaymentID 
                        ON tblProduct.ProductCode = tblPaymentDetails.ProductCode 
                        ON tblInsuree.CHFID = tblPaymentDetails.InsuranceNumber 
                        ON tblControlNumber.PaymentID = tblPayment.PaymentID 
                        LEFT OUTER JOIN tblPremium 
                        LEFT OUTER JOIN tblPolicy 
                        ON tblPremium.PolicyID = tblPolicy.PolicyID 
                        ON tblPaymentDetails.PremiumID = tblPremium.PremiumId
                        WHERE (tblProduct.ValidityTo IS NULL) AND (tblInsuree.ValidityTo IS NULL)
						AND tblPayment.PaymentStatus >= 4 AND tblPayment.PaymentStatus < 5";

            SqlParameter[] parameters = {};

            try
            {
                var data = dh.GetDataTable(sSQl, parameters, CommandType.Text);
                List<MatchSms> Ids = null;
                if (data.Rows.Count > 0)
                {
                    var jsonString = JsonConvert.SerializeObject(data);
                    Ids = JsonConvert.DeserializeObject<List<MatchSms>>(jsonString);                   
                }

                return Ids;
            }
            catch (Exception e)
            {

                throw e;
            }
        }

        public void UnMatchedSmsSent(int Id)
        {
            var sSQL = @"UPDATE tblPayment
                         SET DateLastSMS = @Today
                         WHERE PaymentID = @PaymentID;";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", Id),
                new SqlParameter("@Today",DateTime.UtcNow)
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

        public void MatchedSmsSent()
        {
            var sSQL = @"UPDATE tblPayment
                         SET DateLastSMS = @Today
                         WHERE PaymentID = @PaymentID;";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", PaymentId),
                new SqlParameter("@Today",DateTime.UtcNow)
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

        public string GetPaymentId(string ControlNumber)
        {
            var sSQL = @"SELECT PaymentID FROM tblControlNumber WHERE ControlNumber = @ControlNumber";

            SqlParameter[] parameters = {
                new SqlParameter("@ControlNumber", ControlNumber)
            };
            string paymentId = string.Empty;

            try
            {
                var data = dh.GetDataTable(sSQL, parameters, CommandType.Text);
                if (data.Rows.Count > 0)
                {
                    var row = data.Rows[0];
                    paymentId = row["PaymentID"].ToString();
                }
                //GetPaymentInfo(PaymentID);
            }
            catch (Exception e)
            {
                return string.Empty;
            }

            return paymentId;
        }
        public List<ReconciliationItem> ProvideReconciliationData(ReconciliationRequest model)
        {
            List<ReconciliationItem> result = new List<ReconciliationItem>();

            DateTime startDate = DateTime.ParseExact(model.date_from, "ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture);
            DateTime endDate = DateTime.ParseExact(model.date_to, "ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture);

            SqlParameter[] parameters = {
                new SqlParameter("@DateFrom", startDate),
                new SqlParameter("@DateTo", endDate)
            };

            var sSQL = @"SELECT * FROM tblPayment WHERE ReceivedDate BETWEEN @DateFrom AND @DateTo";

            try
            {
                var data = dh.GetDataTable(sSQL, parameters, CommandType.Text);

                if (data.Rows.Count > 0)
                {
                    for (int i = 0; i < data.Rows.Count; i++)
                    {
                        var rw = data.Rows[i];

                        result.Add(new ReconciliationItem
                        {
                            control_number = rw["PaymentID"].ToString(),
                            enrolment_officer_code = rw["OfficerCode"].ToString(),
                            insurance_number = rw["PaymentID"].ToString(),
                            language = rw["LanguageName"] != System.DBNull.Value ? Convert.ToString(rw["LanguageName"]) : null,
                            payment_date = rw["PaymentDate"] != System.DBNull.Value ? DateTime.Parse(rw["PaymentDate"].ToString()).ToString("ddMMyyyy") : null,
                            payment_origin = rw["PaymentOrigin"].ToString(),
                            receipt_identification = rw["ReceiptNo"].ToString(),
                            received_amount = rw["ReceivedAmount"] != System.DBNull.Value ? Convert.ToDouble(rw["ReceivedAmount"]) : 0,
                            received_date = rw["ReceivedDate"] != System.DBNull.Value ? DateTime.Parse(rw["ReceivedDate"].ToString()).ToString("ddMMyyyy") : null,
                            transaction_identification = rw["TransactionNo"].ToString(),
                            type_of_payment = rw["TypeOfPayment"] != System.DBNull.Value ? Convert.ToString(rw["TypeOfPayment"]) : null
                        });
                    }
                }
            }
            catch (Exception e)
            { }

            return result;
        }

        public void CancelPayment(int payment_id)
        {
            var sSQL = @"UPDATE tblPayment
                         SET PaymentStatus = @PaymentStatus
                         WHERE PaymentID = @PaymentID;";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", payment_id),
                new SqlParameter("@PaymentStatus", PaymentStatus.Cancelled)
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

        public void updateReconciliatedPayment(string billId)
        {
            var sSQL = @"UPDATE tblPayment
                         SET PaymentStatus = @PaymentStatus
                         WHERE PaymentID = @PaymentID;";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", billId),
                new SqlParameter("@PaymentStatus", PaymentStatus.Reconciliated)
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

        public void updateReconciliatedPaymentError(string billId)
        {
            var sSQL = @"UPDATE tblPayment
                         SET PaymentStatus = @PaymentStatus
                         WHERE PaymentID = @PaymentID;";

            SqlParameter[] parameters = {
                new SqlParameter("@PaymentID", billId),
                new SqlParameter("@PaymentStatus", PaymentStatus.FailedReconciliated)
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

        public bool CheckPaymentExistError(string id)
        {
            var sSQL = @"SELECT PaymentID FROM tblPayment WHERE PaymentID = @paymentId And PaymentStatus<>@PaymentStatus And ValidityTo is Null";

            SqlParameter[] parameters = {
                new SqlParameter("@paymentId", id),
                new SqlParameter("@PaymentStatus", PaymentStatus.FailedReconciliated)
            };
            var paymentExist = false; 
            try
            {
                var data = dh.GetDataTable(sSQL, parameters, CommandType.Text);
                if (data.Rows.Count > 0)
                {
                    var row = data.Rows[0];
                    if (row["PaymentID"].ToString()==id) 
                    {
                        paymentExist = true;
                    }
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return paymentExist;
        }


    }
}
