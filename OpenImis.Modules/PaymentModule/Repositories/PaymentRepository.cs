﻿using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OpenImis.DB.SqlServer.DataHelper;
using OpenImis.Modules.PaymentModule.Helpers;
using OpenImis.Modules.PaymentModule.Models;
using OpenImis.Modules.PaymentModule.Models.Response;
using OpenImis.Modules.PolicyModule.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Language = OpenImis.Modules.PaymentModule.Models.Language;

namespace OpenImis.Modules.PaymentModule.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private IConfiguration Configuration;
        protected DataHelper dh;

        public string PaymentId { get; set; }
        public decimal ExpectedAmount { get; set; }
        public string ControlNum { get; set; }
        public string PhoneNumber { get; set; }
        public Language Language { get; set; }
        public TypeOfPayment? typeOfPayment { get; set; }

        public DateTime? PaymentDate { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? OutStAmount { get; set; }
        public List<InsureeProduct> InsureeProducts { get; set; }

        public PaymentRepository(IConfiguration configuration)
        {
            Configuration = configuration;
            dh = new DataHelper(configuration);
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
                        new XElement("RequestDate", _intent.request_date),
                        new XElement("PhoneNumber", _intent.phone_number),
                        new XElement("LanguageName", _intent.language),
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

                var languages = LocalDefault.PrimaryLangReprisantations(Configuration);

                if (_intent.language == null || languages.Contains(_intent.language.ToLower()))
                {
                    Language = Language.Primary;
                }
                else
                {
                    Language = Language.Secondary;
                }

                message = new SaveIntentResponse(rv, error, dt, (int)Language).Message;
                GetPaymentInfo(PaymentId);
            }
            catch (Exception e)
            {

                message = new SaveIntentResponse(e).Message;
            }

            return message;
        }

        public void GetPaymentInfo(string Id)
        {
            var sSQL = @"SELECT tblPayment.PaymentID, tblPayment.ExpectedAmount,tblPayment.LanguageName,tblPayment.TypeOfPayment, tblPaymentDetails.ExpectedAmount AS ExpectedDetailAmount,
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
                    ControlNum = row1["ControlNumber"] != System.DBNull.Value ? Convert.ToString(row1["ControlNumber"]) : null;
                    ExpectedAmount = row1["ExpectedAmount"] != System.DBNull.Value ? Convert.ToDecimal(row1["ExpectedAmount"]) : 0;

                    var language = row1["LanguageName"] != System.DBNull.Value ? Convert.ToString(row1["LanguageName"]) : "en";
                    var languages = LocalDefault.PrimaryLangReprisantations(Configuration);

                    if (language == null || languages.Contains(language.ToLower()))
                    {
                        Language = Language.Primary;
                    }
                    else
                    {
                        Language = Language.Secondary;
                    }
                    typeOfPayment = row1["TypeOfPayment"] != System.DBNull.Value ? (TypeOfPayment?)Enum.Parse(typeof(TypeOfPayment), Convert.ToString(row1["TypeOfPayment"]), true) : null;

                    PhoneNumber = row1["PhoneNumber"] != System.DBNull.Value ? Convert.ToString(row1["PhoneNumber"]) : null;
                    PaymentDate = (DateTime?)(row1["PaymentDate"] != System.DBNull.Value ? row1["PaymentDate"] : null);
                    PaidAmount = (decimal?)(row1["ReceivedAmount"] != System.DBNull.Value ? row1["ReceivedAmount"] : null);
                    OutStAmount = (decimal?)(row1["Outstanding"] != System.DBNull.Value ? row1["Outstanding"] : null);
                    InsureeProducts = new List<InsureeProduct>();

                    for (int i = 0; i < data.Rows.Count; i++)
                    {
                        var rw = data.Rows[i];

                        bool active = false;

                        if (rw["PolicyStatus"] != System.DBNull.Value && Convert.ToInt32(rw["PolicyStatus"]) == 2)
                        {
                            active = true;
                        }

                        var othernames = rw["OtherNames"] != System.DBNull.Value ? Convert.ToString(rw["OtherNames"]) : null;
                        var lastname = rw["LastName"] != System.DBNull.Value ? Convert.ToString(rw["LastName"]) : null;
                        InsureeProducts.Add(
                                new InsureeProduct()
                                {
                                    InsureeNumber = rw["InsuranceNumber"] != System.DBNull.Value ? Convert.ToString(rw["InsuranceNumber"]) : null,
                                    InsureeName = othernames + " " + lastname,
                                    ProductName = rw["ProductName"] != System.DBNull.Value ? Convert.ToString(rw["ProductName"]) : null,
                                    ProductCode = rw["ProductCode"] != System.DBNull.Value ? Convert.ToString(rw["ProductCode"]) : null,
                                    ExpiryDate = (DateTime?)(rw["ExpiryDate"] != System.DBNull.Value ? rw["ExpiryDate"] : null),
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

        public virtual decimal determineTransferFee(decimal expectedAmount, TypeOfPayment typeOfPayment)
        {
            return 0;
        }

        public virtual bool UpdatePaymentTransferFee(string paymentId, decimal TransferFee, TypeOfPayment typeOfPayment)
        {

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
            catch (Exception)
            {
                return false;
            }

        }

        public virtual decimal GetToBePaidAmount(decimal ExpectedAmount, decimal TransferFee)
        {
            decimal amount = ExpectedAmount - TransferFee;
            return Math.Round(amount, 2);
        }

        public virtual PostReqCNResponse PostReqControlNumber(string OfficerCode, string PaymentId, string PhoneNumber, decimal ExpectedAmount, List<PaymentDetail> products, string controlNumber = null, bool acknowledge = false, bool error = false)
        {
            bool result = SaveControlNumberRequest(PaymentId, error);
            string ctrlNumber = null;

            PostReqCNResponse response = new PostReqCNResponse()
            {

                ControlNumber = ctrlNumber,
                Posted = true,
                ErrorCode = 0,
                Assigned = error
            };

            return response;
        }

        public bool SaveControlNumberRequest(string BillId, bool failed)
        {

            SqlParameter[] sqlParameters = {
                new SqlParameter("@PaymentID", BillId),
                new SqlParameter("@Failed", failed)
             };

            try
            {
                var data = dh.ExecProcedure("uspRequestGetControlNumber", sqlParameters);
                GetPaymentInfo(BillId);
            }
            catch (Exception e)
            {
                throw e;
            }

            return true;
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
            catch (Exception)
            {
                return false;
            }

            return result;
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

        public DataMessage SaveControlNumber(string ControlNumber, bool failed)
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

        public DataMessage SaveControlNumber(ControlNumberResp model, bool failed)
        {
            SqlParameter[] sqlParameters = {
                new SqlParameter("@PaymentID", model.internal_identifier),
                new SqlParameter("@ControlNumber", model.control_number),
                new SqlParameter("@Failed", failed)
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
    }
}
