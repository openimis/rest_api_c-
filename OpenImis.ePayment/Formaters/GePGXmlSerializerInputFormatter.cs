using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OpenImis.ePayment.Escape.Payment.Models;
using OpenImis.ePayment.Extensions;
using Newtonsoft.Json;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace OpenImis.ePayment.Formaters
{
    public class GePGXmlSerializerInputFormatter: TextInputFormatter
    {

        private Type type;
        private IConfiguration configuration;
        private IHostingEnvironment hostingEnvironment;

        public GePGXmlSerializerInputFormatter(IHostingEnvironment hostingEnvironment, IConfiguration configuration)
        {
            this.configuration = configuration;
            this.hostingEnvironment = hostingEnvironment;

            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/xml"));

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        #region canreadtype
        protected override bool CanReadType(Type type)
        {
            this.type = type;
            if (type == typeof(gepgPmtSpInfo) || type == typeof(gepgBillSubResp) || type == typeof(gepgSpReconcResp))
            {
                return base.CanReadType(type);
            }
            return false;
        }
        #endregion

        #region readrequest
        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding effectiveEncoding)
        {
            string content;
            string signature;
            bool hasValidSignature = true;

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (effectiveEncoding == null)
            {
                throw new ArgumentNullException(nameof(effectiveEncoding));
            }

            var Request = context.HttpContext.Request;

            var buffer = new byte[Convert.ToInt32(Request.ContentLength)];
            string body = string.Empty;

            using (var reader = new StreamReader(
               Request.Body,
               encoding: effectiveEncoding,
               detectEncodingFromByteOrderMarks: false,
               bufferSize: buffer.Length,
               leaveOpen: true
               ))
            {
                body = await reader.ReadToEndAsync();
                // stream = reader;// Do something
            }

            GePGSignatureValidator signatureValidator = new GePGSignatureValidator(this.hostingEnvironment, this.configuration);
            content = this.getContent(body, this.type.Name);
            signature = this.getSignature(body, "gepgSignature");

            hasValidSignature = signatureValidator.VerifyData(content, signature);
            
            //get the billId/paymentId from request body - from <BillId> node
            if (this.type.Name != "gepgSpReconcResp")
            {
                string billId = StringExtensions.Between(body, "<BillId>", "</BillId>");
                if (!String.IsNullOrEmpty(billId))
                {
                    var gepgFile = new GepgFoldersCreating(billId + "_" + this.type.Name, body, Path.Combine(System.Environment.CurrentDirectory, "wwwroot"));
                    gepgFile.putRequestBody();
                }
                else
                {
                    var gepgFile = new GepgFoldersCreating(this.type.Name, body, Path.Combine(System.Environment.CurrentDirectory, "wwwroot"));
                    gepgFile.putRequestBody();
                }
            }
            else
            {
                var gepgFile = new GepgFoldersCreating(this.type.Name, body, Path.Combine(System.Environment.CurrentDirectory, "wwwroot"));
                gepgFile.putRequestBody();
            }

            TextReader writer = new StringReader(content);

            try
            {
                var serializer = new XmlSerializer(this.type);
                dynamic model = Convert.ChangeType(serializer.Deserialize(writer), this.type);
                model.HasValidSignature = hasValidSignature;
                return await InputFormatterResult.SuccessAsync(Convert.ChangeType(model, this.type));
            }
            catch
            {
                return await InputFormatterResult.FailureAsync();
            }

        }

        public string getContent(string rawData, string dataTag)
        {
            try
            {
                string content = rawData.Substring(rawData.IndexOf(dataTag) - 1, rawData.LastIndexOf(dataTag) + dataTag.Length + 2 - rawData.IndexOf(dataTag));
                return content;
            }
            catch (Exception)
            {

                return string.Empty;
            }

        }

        public string getSignature(string rawData, string sigTag)
        {
            try
            {
                string content = rawData.Substring(rawData.IndexOf(sigTag) + sigTag.Length + 1, rawData.LastIndexOf(sigTag) - rawData.IndexOf(sigTag) - sigTag.Length - 3);
                return content;
            }
            catch (Exception)
            {

                return string.Empty;
            }

        }
        #endregion
    }
}
