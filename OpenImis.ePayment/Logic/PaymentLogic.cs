using OpenImis.ePayment.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenImis.ePayment.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using OpenImis.ePayment.Models.Payment;
using OpenImis.ePayment.Responses;
using OpenImis.ePayment.Models.Sms;
using OpenImis.ePayment.Escape.Sms;
using Newtonsoft.Json;
using OpenImis.ePayment.Models.Payment.Response;
using System.Diagnostics;

namespace OpenImis.ePayment.Logic
{
    public class PaymentLogic
    {

        private IConfiguration _configuration;
        private readonly IHostingEnvironment _hostingEnvironment;

        public PaymentLogic(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;

        }
        public async Task<DataMessage> SaveIntent(IntentOfPay intent, int? errorNumber = 0, string errorMessage = null)
        {

            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);
            var intentResponse = payment.SaveIntent(intent, errorNumber, errorMessage);

            DataMessage return_message = new DataMessage();
            return_message.Code = intentResponse.Code;
            return_message.MessageValue = intentResponse.MessageValue;

            if (intentResponse.Code == 0)
            {
                var objstring = intentResponse.Data.ToString();
                List<AssignedControlNumber> data = JsonConvert.DeserializeObject<List<AssignedControlNumber>>(objstring);
                var ret_data = data.FirstOrDefault();

                decimal transferFee = 0;
                //Get the transfer Fee
                if (intent.type_of_payment != null)
                {
                    transferFee = payment.determineTransferFee(payment.ExpectedAmount, (TypeOfPayment)intent.type_of_payment);

                    var success = payment.UpdatePaymentTransferFee(payment.PaymentId, transferFee, (TypeOfPayment)intent.type_of_payment);

                }

                var amountToBePaid = payment.GetToBePaidAmount(payment.ExpectedAmount, transferFee);
                var response = await payment.PostReqControlNumberAsync(intent.enrolment_officer_code, payment.PaymentId, intent.phone_number, amountToBePaid, intent.policies);

                if (response.ControlNumber != null) // 
                {
                    var controlNumberExists = payment.CheckControlNumber(payment.PaymentId, response.ControlNumber);
                    return_message = payment.SaveControlNumber(response.ControlNumber, controlNumberExists);
                    if (payment.PaymentId != null && intent.SmsRequired)
                    {
                        if (!return_message.ErrorOccured && !controlNumberExists)
                        {
                            ret_data.control_number = response.ControlNumber;
                            ControlNumberAssignedSms(payment);
                        }
                        else
                        {
                            ControlNumberNotassignedSms(payment, return_message.MessageValue);
                        }
                    }
                }
                else 
                    if (response.Posted == true)
                    {
                        return_message = payment.SaveControlNumberAkn(response.ErrorOccured, response.ErrorMessage);
                    }
                    else if (response.ErrorOccured == true)
                    {
                        return_message = payment.SaveControlNumberAkn(response.ErrorOccured, response.ErrorMessage);
                        ControlNumberNotassignedSms(payment, response.ErrorMessage);

                    }

                return_message.Data = ret_data;

            }
            else
            {
                return_message = intentResponse;
                return_message.Data = new AssignedControlNumber();
            }

            return return_message;
        }

        public async Task<DataMessage> MatchPayment(MatchModel model)
        {
            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);
            var response = payment.MatchPayment(model);

            if (model.internal_identifier == null)
            {
                List<MatchSms> PaymentIds = new List<MatchSms>();

                PaymentIds = payment.GetPaymentIdsForSms();

                if (PaymentIds != null)
                    SendMatchSms(PaymentIds);
            }
            else
            {
                var matchdata = JsonConvert.SerializeObject(response.Data);
                var matchedPayments = JsonConvert.DeserializeObject<List<MatchedPayment>>(matchdata);

                if (matchedPayments.FirstOrDefault().PaymentMatched > 0)
                {
                    SendPaymentSms(payment);
                }

            }

            return response;
        }

        public DataMessage SaveAcknowledgement(Acknowledgement model)
        {
            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment) { PaymentId = model.internal_identifier };
            var response = payment.SaveControlNumberAkn(model.error_occured, model.error_message);

            return response;
        }

        public async Task<DataMessage> SavePayment(PaymentData model)
        {

            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);
            //var controlNumberExists = payment.CheckControlNumber(model.internal_identifier, model.control_number);

            if (model.control_number != null)
            {
                model.type_of_payment = null;

                var paymentId = payment.GetPaymentId(model.control_number);

                if (paymentId != null && paymentId != string.Empty)
                {
                    payment.GetPaymentInfo(paymentId);
                }
                else
                {
                    DataMessage dm = new DataMessage
                    {
                        Code = 3,
                        ErrorOccured = true,
                        MessageValue = "3-Wrong control_number",
                    };

                    return dm;
                }
            }


            if (model.type_of_payment == null && payment.typeOfPayment != null)
            {
                var transferFee = payment.determineTransferFeeReverse(Convert.ToDecimal(model.received_amount), (TypeOfPayment)payment.typeOfPayment);
                var success = payment.UpdatePaymentTransferFee(payment.PaymentId, transferFee, (TypeOfPayment)payment.typeOfPayment);
                model.received_amount = model.received_amount + Convert.ToDouble(transferFee);
            }
            else if (model.type_of_payment != null && payment.typeOfPayment == null)
            {
                var transferFee = payment.determineTransferFeeReverse(Convert.ToDecimal(model.received_amount), (TypeOfPayment)model.type_of_payment);
                var success = payment.UpdatePaymentTransferFee(payment.PaymentId, transferFee, (TypeOfPayment)model.type_of_payment);
                model.received_amount = model.received_amount + Convert.ToDouble(transferFee);
            }

            var response = payment.SavePayment(model);



            if (payment.PaymentId != null && !response.ErrorOccured)
            {
                var ackResponse = payment.GetPaymentDataAck(payment.PaymentId, payment.ControlNum);

                MatchModel matchModel = new MatchModel() { internal_identifier = payment.PaymentId, audit_user_id = -3 };

                var matchresponse = await MatchPayment(matchModel);

#if DEBUG
                var matchdata = JsonConvert.SerializeObject(matchresponse.Data);
                var matchedPayments = JsonConvert.DeserializeObject<List<MatchedPayment>>(matchdata);

                if (matchedPayments.Select(x => x.PaymentId).Contains(payment.PaymentId))
                {
                    SendPaymentSms(payment);
                }
#endif

            }

            return response;
        }

        public DataMessage SaveControlNumber(ControlNumberResp model)
        {

            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);
            var controlNumberExists = payment.CheckControlNumber(model.internal_identifier, model.control_number);
            var response = payment.SaveControlNumber(model, controlNumberExists);

            if (model.error_occured)
            {
                response.ErrorOccured = true;
                response.MessageValue = model.error_message;
            }

            if (payment.PaymentId != null && payment.SmsRequired)
            {
                var ackResponse = payment.GetReqControlNumberAck(payment.PaymentId);

                if (!response.ErrorOccured && !controlNumberExists)
                {
                    ControlNumberAssignedSms(payment);
                }
                else
                {
                    ControlNumberNotassignedSms(payment, response.MessageValue);
                }
            }

            return response;
        }

        public async void ControlNumberAssignedSms(ImisPayment payment)
        {
            //Language lang = payment.Language.ToLower() == "en" || payment.Language.ToLower() == "english" || payment.Language.ToLower() == "primary" ? Language.Primary : Language.Secondary;
            ImisSms sms = new ImisSms(_configuration, _hostingEnvironment, payment.Language);
            var txtmsgTemplate = string.Empty;
            string othersCount = string.Empty;

            if (payment.InsureeProducts.Count > 1)
            {
                txtmsgTemplate = sms.GetMessage("ControlNumberAssignedV2");
                othersCount = Convert.ToString(payment.InsureeProducts.Count - 1);
            }
            else
            {
                txtmsgTemplate = sms.GetMessage("ControlNumberAssigned");
            }

            decimal transferFee = 0;

            if (payment.typeOfPayment != null)
            {
                transferFee = payment.determineTransferFee(payment.ExpectedAmount, (TypeOfPayment)payment.typeOfPayment);

            }

            var txtmsg = string.Format(txtmsgTemplate,
                payment.ControlNum,
                DateTime.UtcNow.ToString("dd-MM-yyyy"),
                DateTime.UtcNow.ToString("dd-MM-yyyy"),
                payment.InsureeProducts.FirstOrDefault().InsureeNumber,
                payment.InsureeProducts.FirstOrDefault().InsureeName,
                payment.InsureeProducts.FirstOrDefault().ProductCode,
                payment.InsureeProducts.FirstOrDefault().ProductName,
                payment.GetToBePaidAmount(payment.ExpectedAmount, transferFee),
                othersCount);

            List<SmsContainer> message = new List<SmsContainer>();
            message.Add(new SmsContainer() { Message = txtmsg, Recipient = payment.PhoneNumber });

            var fileName = "CnAssigned_" + payment.PhoneNumber;

            string test = await sms.SendSMS(message, fileName);
        }

        public async Task<DataMessage> GetControlNumbers(PaymentRequest requests)
        {
            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);
            var PaymentIds = requests.requests.Select(x => x.internal_identifier).ToArray();

            var PaymentIds_string = string.Join(",", PaymentIds);

            var response = await payment.GetControlNumbers(PaymentIds_string);

            return response;
        }

        public async void ControlNumberNotassignedSms(ImisPayment payment, string error)
        {
            //Language lang = payment.Language.ToLower() == "en" || payment.Language.ToLower() == "english" || payment.Language.ToLower() == "primary" ? Language.Primary : Language.Secondary;
            ImisSms sms = new ImisSms(_configuration, _hostingEnvironment, payment.Language);
            var txtmsgTemplate = string.Empty;
            string othersCount = string.Empty;

            if (payment.InsureeProducts.Count > 1)
            {
                txtmsgTemplate = sms.GetMessage("ControlNumberErrorV2");
                othersCount = Convert.ToString(payment.InsureeProducts.Count - 1);
            }
            else
            {
                txtmsgTemplate = sms.GetMessage("ControlNumberError");
            }

            var txtmsg = string.Format(txtmsgTemplate,
                payment.ControlNum,
                DateTime.UtcNow.ToString("dd-MM-yyyy"),
                DateTime.UtcNow.ToString("dd-MM-yyyy"),
                payment.InsureeProducts.FirstOrDefault().InsureeNumber,
                payment.InsureeProducts.FirstOrDefault().ProductCode,
                error,
                othersCount);

            List<SmsContainer> message = new List<SmsContainer>();
            message.Add(new SmsContainer() { Message = txtmsg, Recipient = payment.PhoneNumber });

            var fileName = "CnError_" + payment.PhoneNumber;

            string test = await sms.SendSMS(message, fileName);
        }

        public async void SendPaymentSms(ImisPayment payment)
        {
            // Language lang = payment.Language.ToLower() == "en" || payment.Language.ToLower() == "english" || payment.Language.ToLower() == "primary" ? Language.Primary : Language.Secondary;
            ImisSms sms = new ImisSms(_configuration, _hostingEnvironment, payment.Language);
            List<SmsContainer> message = new List<SmsContainer>();
            var familyproduct = payment.InsureeProducts.FirstOrDefault();

            if (familyproduct.PolicyActivated)
            {

                var txtmsg = string.Format(sms.GetMessage("PaidAndActivated"),
                    payment.PaidAmount,
                    DateTime.UtcNow.ToString("dd-MM-yyyy"),
                    payment.ControlNum,
                    familyproduct.InsureeNumber,
                    familyproduct.InsureeName,
                    familyproduct.ProductCode,
                    familyproduct.ProductName,
                    familyproduct.EffectiveDate.Value.ToShortDateString(),
                    familyproduct.ExpiryDate.Value.ToShortDateString(),
                    payment.PaidAmount);


                message.Add(new SmsContainer() { Message = txtmsg, Recipient = payment.PhoneNumber });

            }
            else
            {

                decimal transferFee = 0;

                if (payment.typeOfPayment != null)
                {
                    transferFee = payment.determineTransferFee(payment.ExpectedAmount, (TypeOfPayment)payment.typeOfPayment);
                }

                var txtmsg = string.Format(sms.GetMessage("PaidAndNotActivated"),
                    payment.PaidAmount,
                    DateTime.UtcNow.ToString("dd-MM-yyyy"),
                    payment.ControlNum,
                    familyproduct.InsureeNumber,
                    familyproduct.InsureeName,
                    familyproduct.ProductCode,
                    familyproduct.ProductName,
                    payment.GetToBePaidAmount(payment.ExpectedAmount,transferFee),
                    payment.OutStAmount);

                message.Add(new SmsContainer() { Message = txtmsg, Recipient = payment.PhoneNumber });

            }
            var fileName = "PayStatSms_" + payment.PhoneNumber;

            string test = await sms.SendSMS(message, fileName);
            payment.MatchedSmsSent();
        }

        public async void SendMatchSms(ImisPayment payment)
        {
            // Language lang = payment.Language.ToLower() == "en" || payment.Language.ToLower() == "english" || payment.Language.ToLower() == "primary" ? Language.Primary : Language.Secondary;
            ImisSms sms = new ImisSms(_configuration, _hostingEnvironment, payment.Language);
            List<SmsContainer> message = new List<SmsContainer>();

            var txtmsgTemplate = string.Empty;
            string othersCount = string.Empty;

            if (payment.InsureeProducts.Count > 1)
            {
                txtmsgTemplate = sms.GetMessage("PaidAndNotMatchedV2");
                othersCount = Convert.ToString(payment.InsureeProducts.Count - 1);
            }
            else
            {
                txtmsgTemplate = sms.GetMessage("PaidAndNotMatched");
            }
            var familyproduct = payment.InsureeProducts.FirstOrDefault();
            var txtmsg = string.Format(txtmsgTemplate,
                     payment.PaidAmount,
                     DateTime.UtcNow.ToString("dd-MM-yyyy"),
                     payment.ControlNum,
                     familyproduct.InsureeNumber,
                     familyproduct.InsureeName,
                     familyproduct.ProductCode,
                     familyproduct.ProductName,
                     othersCount);


            message.Add(new SmsContainer() { Message = txtmsg, Recipient = payment.PhoneNumber });

            var fileName = "PayNotMatched_" + payment.PhoneNumber;

            string test = await sms.SendSMS(message, fileName);
        }

        public async void SendMatchSms(List<MatchSms> Ids)
        {

            List<SmsContainer> message = new List<SmsContainer>();

            foreach (var m in Ids)
            {
                bool shoulSendSms = LocalDefault.ShouldSendSms(_configuration, m.DateLastSms, m.MatchedDate);

                if (shoulSendSms)
                {
                    var txtmsgTemplate = string.Empty;
                    string othersCount = string.Empty;

                    ImisPayment _pay = new ImisPayment(_configuration, _hostingEnvironment);
                    _pay.GetPaymentInfo(m.PaymentId.ToString());

                    //Language lang = _pay.Language.ToLower() == "en" || _pay.Language.ToLower() == "english" || _pay.Language.ToLower() == "primary" ? Language.Primary : Language.Secondary;
                    ImisSms sms = new ImisSms(_configuration, _hostingEnvironment, _pay.Language);

                    if (_pay.PaymentId != null)
                    {
                        if (_pay.InsureeProducts.Count > 1)
                        {
                            txtmsgTemplate = sms.GetMessage("PaidAndNotMatchedV2");
                            othersCount = Convert.ToString(_pay.InsureeProducts.Count - 1);
                        }
                        else
                        {
                            txtmsgTemplate = sms.GetMessage("PaidAndNotMatched");
                        }
                        var familyproduct = _pay.InsureeProducts.FirstOrDefault();
                        var txtmsg = string.Format(txtmsgTemplate,
                                 _pay.PaidAmount,
                                 DateTime.UtcNow.ToString("dd-MM-yyyy"),
                                 _pay.ControlNum,
                                 familyproduct.InsureeNumber,
                                 familyproduct.InsureeName,
                                 familyproduct.ProductCode,
                                 familyproduct.ProductName,
                                 othersCount);


                        message.Add(new SmsContainer() { Message = txtmsg, Recipient = _pay.PhoneNumber });
                        _pay.UnMatchedSmsSent(m.PaymentId);
                    }
                    else
                    {
                        throw new Exception();
                    }

                    var fileName = "PayNotMatched_";
                    string test = await sms.SendSMS(message, fileName);
                }

            }

        }

        public async Task<ReconciliationMessage> ProvideReconciliationData(ReconciliationRequest model)
        {
            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);

            var response = payment.ProvideReconciliationData(model);

            ReconciliationMessage return_message = new ReconciliationMessage();

            return_message.transactions = response;
            return_message.error_occurred = false;

            return return_message;
        }

        public async Task<DataMessage> CancelPayment(PaymentCancelModel model)
        {

            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);
            DataMessage dt = new DataMessage();
            
            if (model.control_number != null)
            {
                // todo: change string type to int
                string paymentId = payment.GetPaymentId(model.control_number);

                if (paymentId != null && paymentId != string.Empty)
                {
                    var response = payment.GePGPostCancelPayment(int.Parse(paymentId));

                    dt.Data = response;

                    payment.CancelPayment(int.Parse(paymentId));
                }
                else
                {
                    //Todo: move hardcoded message to translation file
                    DataMessage dm = new DataMessage
                    {
                        Code = 2,
                        ErrorOccured = true,
                        MessageValue = "CancelPayment:2:Control Number doesn't exists",
                    };

                    return dm;
                }
            }
            else
            {
                DataMessage dm = new DataMessage
                {
                    Code = 1,
                    ErrorOccured = true,
                    MessageValue = "CancelPayment:1:Missing Control Number",
                };

                return dm;
            }


            return dt;
        }
    }
}
