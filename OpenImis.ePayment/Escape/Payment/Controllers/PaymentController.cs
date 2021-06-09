using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using OpenImis.ePayment.Escape;
using OpenImis.ePayment.Escape.Payment.Models;
using OpenImis.ePayment.Data;
using OpenImis.ePayment.Extensions;
using OpenImis.ePayment.Formaters;
using OpenImis.ePayment.Models;
using OpenImis.ePayment.Models.Payment;
using OpenImis.ePayment.Models.Payment.Response;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenImis.ePayment.Responses;

namespace OpenImis.ePayment.Controllers
{
    [ApiVersion("2")]
    public class PaymentController : PaymentBaseController
    {
        private ImisPayment imisPayment;
        private IHostingEnvironment env;

        public PaymentController(IConfiguration configuration, IHostingEnvironment hostingEnvironment) : base(configuration, hostingEnvironment)
        {
            imisPayment = new ImisPayment(configuration, hostingEnvironment);
            env = hostingEnvironment;
        }

#if CHF

        #region Step 1: Control Number Management 

        [HttpPost]
        [Route("api/GetControlNumber")]
        [ProducesResponseType(typeof(GetControlNumberResp), 200)]
        [ProducesResponseType(typeof(ErrorResponseV2), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CHFRequestControlNumberForMultiplePolicies([FromBody] IntentOfPay intent)
        {
            return await base.GetControlNumber(intent);
        }

        [HttpPost]
        [Route("api/GetControlNumber/Single")]
        public async Task<IActionResult> CHFRequestControlNumberForSimplePolicy([FromBody]IntentOfSinglePay intent)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.FirstOrDefault().Errors.FirstOrDefault().ErrorMessage;
                return BadRequest(new { success = false, message = error, error_occured = true, error_message = error });
            }

            intent.phone_number = intent.Msisdn;
            intent.enrolment_officer_code = intent.OfficerCode;
            intent.SmsRequired = true;

            if (intent.enrolment_officer_code == null)
                intent.EnrolmentType = EnrolmentType.Renewal + 1;

            intent.SetDetails();

            return await base.GetControlNumber(intent);

        }

        [HttpPost]
        [Route("api/GetReqControlNumber")]
        public IActionResult GePGReceiveControlNumber([FromBody] gepgBillSubResp model)
        {
            int billId;
            if (model.HasValidSignature)
            {
                if (!ModelState.IsValid)
                    return BadRequest(imisPayment.ControlNumberResp(GepgCodeResponses.GepgResponseCodes["Invalid Request Data"]));

                ControlNumberResp ControlNumberResponse;
                foreach (var bill in model.BillTrxInf)
                {
                    ControlNumberResponse = new ControlNumberResp()
                    {
                        internal_identifier = bill.BillId,
                        control_number = bill.PayCntrNum,
                        error_occured = bill.TrxStsCode == GepgCodeResponses.GepgResponseCodes["Successful"].ToString()?false:true,
                        error_message = bill.TrxStsCode
                    };
                
                    billId = bill.BillId;

                    string reconc = JsonConvert.SerializeObject(ControlNumberResponse);
                    GepgFileLogger.Log(billId, "CN_Response", reconc, env);

                    try
                    {
                        var response = base.GetReqControlNumber(ControlNumberResponse);
                        if (ControlNumberResponse.error_occured == true)
                        {
                            var rejectedReason = imisPayment.PrepareRejectedReason(billId, bill.TrxStsCode);
                            imisPayment.setRejectedReason(billId, rejectedReason);
                        }
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                }

                return Ok(imisPayment.ControlNumberResp(GepgCodeResponses.GepgResponseCodes["Successful"]));
            }
            else
            {
                foreach (var bill in model.BillTrxInf)
                {
                    billId = bill.BillId;

                    string reconc = JsonConvert.SerializeObject(model);
                    GepgFileLogger.Log(billId, "CN_Response_InvalidSignature", reconc, env);
                    imisPayment.setRejectedReason(billId, GepgCodeResponses.GepgResponseCodes["Invalid Signature"] + ":Invalid Signature");
                }
                
                return Ok(imisPayment.ControlNumberResp(GepgCodeResponses.GepgResponseCodes["Invalid Signature"]));
            }

        }

        [NonAction]
        public override Task<IActionResult> GetControlNumber([FromBody] IntentOfPay intent)
        {
            return base.GetControlNumber(intent);
        }

        [NonAction]
        public override IActionResult GetReqControlNumber([FromBody] ControlNumberResp model)
        {
            return base.GetReqControlNumber(model);
        }

        [NonAction]
        public override IActionResult PostReqControlNumberAck([FromBody] Acknowledgement model)
        {
            return base.PostReqControlNumberAck(model);
        }

        #endregion

        #region Step 2 (optional): Cancel Payment 
        [HttpPost]
        [Route("api/payment/cancel")]
        [ProducesResponseType(typeof(DataMessage), 200)]
        [ProducesResponseType(typeof(ErrorResponseV2), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CHFCancelOnePayment([FromBody] PaymentCancelModel model)
        {
            ImisPayment payment = new ImisPayment(_configuration, _hostingEnvironment);
            DataMessage dt = new DataMessage();

            if (model.control_number != null)
            {
                // todo: change string type to int
                int paymentId = payment.GetPaymentId(model.control_number);

                if (paymentId != 0)
                {
                    var ack = await payment.GePGPostCancelPayment(paymentId);

                    if (ack.GetType() == typeof(DataMessage))
                    {
                        return Ok((DataMessage)ack);
                    }

                    GePGPaymentCancelResponse response = (GePGPaymentCancelResponse)ack;

                    if (response.gepgBillCanclResp.BillCanclTrxDt.Count() > 0 && 
                        response.gepgBillCanclResp.BillCanclTrxDt[0].TrxSts == TrxSts.Success)
                    {
                        return await base.CancelPayment(model);
                    }
                    else
                    {
                        return Ok(new DataMessage
                        {
                            Data = response,
                            Code = 3,
                            ErrorOccured = true,
                            MessageValue = "CancelPayment:3:Failed to cancel"
                        });
                    }
                }
                else
                {
                    //Todo: move hardcoded message to translation file
                    return Ok(new DataMessage
                    {
                        Code = 2,
                        ErrorOccured = true,
                        MessageValue = "CancelPayment:2:Control Number doesn't exists",
                    });
                }
            }
            
            return Ok(new DataMessage
                {
                    Code = 1,
                    ErrorOccured = true,
                    MessageValue = "CancelPayment:1:Missing Control Number",
                });
            
        }

        [NonAction]
        public override async Task<IActionResult> CancelPayment([FromBody] PaymentCancelBaseModel model)
        {
            return await base.CancelPayment(model);
        }

        #endregion

        #region Step 3: Payment Reception Managent
        [HttpPost]
        [Route("api/GetPaymentData")]
        public async Task<IActionResult> GetPaymentChf([FromBody] gepgPmtSpInfo model)
        {
            int billId;
            if (model.HasValidSignature)
            {
                if (!ModelState.IsValid)
                    return BadRequest(imisPayment.PaymentResp(GepgCodeResponses.GepgResponseCodes["Invalid Request Data"]));

                object _response = null;
                int errorCode = 0;
                foreach (var payment in model.PymtTrxInf)
                {

                    PaymentData pay = new PaymentData()
                    {
                        control_number = payment.PayCtrNum,
                        //insurance_product_code = payment.PayRefId,
                        enrolment_officer_code = payment.PyrName,
                        transaction_identification = payment.TrxId,
                        received_amount = Convert.ToDouble(payment.PaidAmt),
                        received_date = DateTime.UtcNow.ToString("yyyy/MM/dd"),
                        payment_date = payment.TrxDtTm,
                        payment_origin = payment.PspName,
                        receipt_identification = payment.PayRefId,
                        payer_phone_number = payment.PyrCellNum
                    };

                    billId = payment.BillId;

                    string reconc = JsonConvert.SerializeObject(_response);
                    GepgFileLogger.Log(billId, "Payment", reconc, env);
                    
                    _response = await base.GetPaymentData(pay);
                    var errorValue = _response.GetType().GetProperty("Value").GetValue(_response);
                    //check if response contains 'error_message' property after processing payment from GePG
                    if (errorValue.GetType().GetProperty("error_message") != null)
                    {
                        string errorMessage = (errorValue.GetType().GetProperty("error_message").GetValue(errorValue)).ToString();
                        if (errorMessage != "Unknown Error Occured" && errorMessage != "3-Wrong control_number")
                        {
                            string[] errorCodes = errorMessage.Split(':');
                            //get the error code returned after processing payment from GePG
                            errorCode = int.Parse(errorCodes[0]);
                        }
                        else
                        {
                            //otherwise in case of another errors - raise '7201:Failure'
                            errorCode = GepgCodeResponses.GepgResponseCodes["Failure"];
                        }
                    }
                }

                if (errorCode == 0)
                {
                    return Ok(imisPayment.PaymentResp(GepgCodeResponses.GepgResponseCodes["Successful"]));
                }
                else 
                {
                    //error code saved in DataMessage
                    return Ok(imisPayment.PaymentResp(errorCode));
                }
            }
            else
            {
                foreach (var payment in model.PymtTrxInf)
                {
                    billId = payment.BillId;

                    string reconc = JsonConvert.SerializeObject(model);
                    GepgFileLogger.Log(billId, "PaymentInvalidSignature", reconc, env);
                    imisPayment.setRejectedReason(billId, GepgCodeResponses.GepgResponseCodes["Invalid Signature"] + ":Invalid Signature");

                }

                return Ok(imisPayment.PaymentResp(GepgCodeResponses.GepgResponseCodes["Invalid Signature"]));
            }

        }

        [NonAction]
        public override async Task<IActionResult> GetPaymentData([FromBody] PaymentData model)
        {
            return await base.GetPaymentData(model);
        }
        #endregion

        #region Step 4: Payments Reconciliation Managent
        [HttpGet]
        [Route("api/Reconciliation")]
        public IActionResult Reconciliation(int daysAgo)
        {
            List<object> done = new List<object>();
            // Make loop for all product from database that have account follow SP[0-9]{3} and do the function for all sp codes
            var productsSPCodes = imisPayment.GetProductsSPCode();
            if (productsSPCodes.Count > 0)
            {
                foreach (String productSPCode in productsSPCodes)
                {
                    var result = imisPayment.RequestReconciliationReportAsync(daysAgo, productSPCode);
                    //check if we have done result - if no - then return 500
                    System.Reflection.PropertyInfo pi = result.GetType().GetProperty("resp");
                    done.Add(result);
                }
            }
            else
            {
                //return not found - no sp codes to proceed 
                return NotFound();
            }
            return Ok(done);
        }
        
        [HttpPost]
        [Route("api/GetReconciliationData")]
        public IActionResult GetReconciliation([FromBody] gepgSpReconcResp model)
        {
            if (model.HasValidSignature) 
            { 
                if (!ModelState.IsValid)
                    return BadRequest(imisPayment.ReconciliationResp(GepgCodeResponses.GepgResponseCodes["Invalid Request Data"]));

                string reconc = JsonConvert.SerializeObject(model);
                GepgFileLogger.Log("Reconc_Data", reconc, env);
                
                foreach (var recon in model.ReconcTrxInf)
                {
                    var paymentToCompare = imisPayment.GetPaymentToReconciliate(recon);
                    if (paymentToCompare != null)
                    {
                        int paymentStatus = (int)paymentToCompare.GetType().GetProperty("paymentStatus").GetValue(paymentToCompare);
                        if (paymentStatus < PaymentStatus.Reconciliated)
                        {
                            imisPayment.updateReconciliatedPayment(recon.SpBillId);
                            //TODO update policy
                        }
                    }
                    else
                    {
                        //send error if payment from GePG not found in IMIS
                        if (imisPayment.CheckPaymentExistError(recon.SpBillId))
                        {
                            imisPayment.updateReconciliatedPaymentError(recon.SpBillId);
                            imisPayment.setRejectedReason(int.Parse(recon.SpBillId), GepgCodeResponses.GepgResponseCodes["No payment(s) found for specified bill control number"] + ":No payment(s) found for specified bill control number");
                        }
                    }

                }
                return Ok(imisPayment.ReconciliationResp(GepgCodeResponses.GepgResponseCodes["Successful"]));
            }
            else
            {
                string reconc = JsonConvert.SerializeObject(model);
                GepgFileLogger.Log("Reconc_DataInvalidSig", reconc, env);

                foreach (var recon in model.ReconcTrxInf)
                {
                    imisPayment.setRejectedReason(int.Parse(recon.SpBillId), GepgCodeResponses.GepgResponseCodes["Invalid Signature"] + ":Invalid Signature");
                }

                return Ok(imisPayment.ReconciliationResp(GepgCodeResponses.GepgResponseCodes["Invalid Signature"]));
            }
        }

        [HttpPost]
        [Route("api/WebMatchPayment")]
        public async Task<IActionResult> WebMatchPayment([FromBody]WebMatchModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error_occured = true, error_message = ModelState.Values.FirstOrDefault().Errors.FirstOrDefault().ErrorMessage });

            // Todo: remove api_key check because is checked by authentication layer
            if (model.api_key != "Xdfg8796021ff89Df4654jfjHeHidas987vsdg97e54ggdfHjdt")
                return BadRequest(new { error_occured = true, error_message = "Unauthorized request" });

            try
            {
                MatchModel match = new MatchModel()
                {
                    internal_identifier = model.internal_identifier,
                    audit_user_id = model.audit_user_id
                };

                var response = await base.MatchPayment(match);
                return Ok(response);
            }
            catch (Exception)
            {
                return BadRequest(new { error_occured = true, error_message = "Unknown Error Occured" });
            }

        }
        #endregion
#endif
    }
}