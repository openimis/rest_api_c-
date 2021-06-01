using OpenImis.ePayment.Escape.Payment.Models;
using OpenImis.ePayment.Data;
using OpenImis.ePayment.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace OpenImis.ePayment.Data
{
    public class GepgUtility
    {
        string PublicStorePath = string.Empty;
        string PrivateStorePath = string.Empty;
        string GepgPayCertStorePath = string.Empty;

        string CertPass = string.Empty;
        
        RSA rsaCrypto = null;
        gepgBillSubReq newBill = null;
        private IConfiguration configuration;

        public GepgUtility(IHostingEnvironment hostingEnvironment, IConfiguration Configuration)
        {
            configuration = Configuration;

            PublicStorePath = Path.Combine(hostingEnvironment.ContentRootPath + Configuration["PaymentGateWay:GePG:PublicStorePath"]);
            PrivateStorePath = Path.Combine(hostingEnvironment.ContentRootPath + Configuration["PaymentGateWay:GePG:PrivateStorePath"]);
            GepgPayCertStorePath = Path.Combine(hostingEnvironment.ContentRootPath + Configuration["PaymentGateWay:GePG:GepgPayCertStorePath"]);

            CertPass = Configuration["PaymentGateWay:GePG:CertPass"];
            
        }

        public String CreateBill(IConfiguration Configuration, string OfficerCode, string PhoneNumber, string BillId, decimal ExpectedAmount, List<InsureeProduct> products)
        {

            DataHelper dh = new DataHelper(Configuration);

            List<BillItem> items = new List<BillItem>();

            BillItem item = new BillItem()
            {
                BillItemRef = "ImisPolicy",
                BillItemAmt = Convert.ToDouble(ExpectedAmount),
                BillItemEqvAmt = Convert.ToDouble(ExpectedAmount),
                BillItemMiscAmt = 0,
                UseItemRefOnPay = "N",
                GfsCode = Configuration["PaymentGateWay:GePG:GfsCode:0"]
            };

            items.Add(item);

            //foreach (var product in products)
            //{

            //    BillItem item = new BillItem()
            //    {
            //        BillItemRef = product.ProductCode,
            //        BillItemAmt = Convert.ToDouble(product.ExpectedProductAmount),
            //        BillItemEqvAmt = Convert.ToDouble(product.ExpectedProductAmount),
            //        BillItemMiscAmt = 0,
            //        UseItemRefOnPay = "N",
            //        GfsCode = Configuration["PaymentGateWay:GePG:GfsCode:0"]
            //    };

            //    items.Add(item);
            //}

            BillTrxInf billTrxInf = new BillTrxInf()
            {
                BillId = BillId,
                SubSpCode = Convert.ToInt32(Configuration["PaymentGateWay:GePG:SubSpCode"]),
                SpSysId = Configuration["PaymentGateWay:GePG:SystemId"],
                Ccy = "TZS",
                BillPayOpt = 3,
                BillGenDt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                BillEqvAmt = Convert.ToDecimal(ExpectedAmount),
                RemFlag = true,
                BillExprDt = DateTime.Now.AddMonths(1).ToString("yyyy-MM-ddTHH:mm:ss"),
                BillAmt = Convert.ToDecimal(ExpectedAmount),
                MiscAmt = 0,
                BillItems = items,
                BillDesc = "Bill",
                BillApprBy = "Imis",
                BillGenBy = "Imis"
            };

            if (OfficerCode == null)
            {
                var InsureeNumber = products.FirstOrDefault().InsureeNumber;

                var sSQL = @"SELECT CHFID,LastName,OtherNames,Phone,Email
                             FROM tblInsuree WHERE CHFID = @InsureeNumber";

                SqlParameter[] parameters = {
                        new SqlParameter("@InsureeNumber", InsureeNumber),
                };

                var data = new DataHelper(Configuration);
                DataTable dt = data.GetDataTable(sSQL, parameters, CommandType.Text);

                billTrxInf.PyrId = InsureeNumber;
                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    billTrxInf.PyrName = Convert.ToString(row["LastName"]) + " " + Convert.ToString(row["OtherNames"]);
                    billTrxInf.PyrEmail = "info@imis.co.tz";
                    billTrxInf.PyrCellNum = Convert.ToString(row["Phone"]).Length == 0 ? PhoneNumber : Convert.ToString(row["Phone"]);
                }
                else
                {
                    billTrxInf.PyrName = InsureeNumber;
                    billTrxInf.PyrEmail = "info@imis.co.tz"; // TODO: replace with insuree's email if exists
                    billTrxInf.PyrCellNum = PhoneNumber;
                }
            }
            else
            {
                var sSQL = @"SELECT Code,LastName,OtherNames,DOB,Phone,EmailId
                            FROM tblOfficer WHERE Code = @OfficerCode";
                SqlParameter[] parameters = {
                        new SqlParameter("@OfficerCode", OfficerCode),
                };

                var data = new DataHelper(Configuration);
                DataTable dt = data.GetDataTable(sSQL, parameters, CommandType.Text);

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];

                    billTrxInf.PyrId = OfficerCode;
                    billTrxInf.PyrName = Convert.ToString(row["LastName"]) + " " + Convert.ToString(row["OtherNames"]);
                    billTrxInf.PyrEmail = "info@imis.co.tz"; // TODO: replace with officer's email 
                    billTrxInf.PyrCellNum = Convert.ToString(row["Phone"]).Length == 0 ? PhoneNumber : Convert.ToString(row["Phone"]);
                }

            }

            string accountCode = GetAccountCodeByProductCode(products.FirstOrDefault().ProductCode);

            newBill = new gepgBillSubReq()
            {
                BillHdr = new BillHdr() { SpCode = accountCode, RtrRespFlg = true },
                BillTrxInf = billTrxInf
            };

            XmlSerializer xs = null;
            XmlSerializerNamespaces ns = null;
            XmlWriterSettings settings = null;
            String outString = String.Empty;

            XmlWriter xw = null;

            try
            {
                settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                //settings.Indent = true;
                ns = new XmlSerializerNamespaces();
                ns.Add("", "");

                StringBuilder sb = new StringBuilder();
                xs = new XmlSerializer(typeof(gepgBillSubReq));

                xw = XmlWriter.Create(sb, settings);

                xs.Serialize(xw, newBill, ns);
                xw.Flush();
                outString = sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                if (xw != null)
                {
                    xw.Close();
                }
            }
            return outString;
        }

        public String CreateGePGCancelPaymentRequest(IConfiguration Configuration, int PaymentId)
        {

            gepgBillCanclReq gepgBillCanclReq = new gepgBillCanclReq()
            {
                SpCode = GetAccountCodeByPaymentId(PaymentId),
                BillId = PaymentId.ToString(),
                SpSysId = Configuration["PaymentGateWay:GePG:SystemId"],
            };

            XmlSerializer xs = null;
            XmlSerializerNamespaces ns = null;
            XmlWriterSettings settings = null;
            String outString = String.Empty;

            XmlWriter xw = null;

            try
            {
                settings = new XmlWriterSettings()
                {
                    OmitXmlDeclaration = true
                };
                
                ns = new XmlSerializerNamespaces();
                ns.Add("", "");

                StringBuilder sb = new StringBuilder();
                xs = new XmlSerializer(typeof(gepgBillCanclReq));

                xw = XmlWriter.Create(sb, settings);

                xs.Serialize(xw, gepgBillCanclReq, ns);
                xw.Flush();

                outString = sb.ToString();

                var signature = this.GenerateSignature(outString);

                GePGPaymentCancelRequest GePGPaymentCancelRequest = new GePGPaymentCancelRequest() 
                { 
                    gepgBillCanclReq = gepgBillCanclReq, 
                    gepgSignature = signature 
                };

                settings = new XmlWriterSettings();
                sb = new StringBuilder();
                xs = new XmlSerializer(typeof(GePGPaymentCancelRequest));
                xw = XmlWriter.Create(sb, settings);

                xs.Serialize(xw, GePGPaymentCancelRequest, ns);
                xw.Flush();
                outString = sb.ToString();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                if (xw != null)
                {
                    xw.Close();
                }
            }
            return outString;
        }

        public string FinaliseSignedMsg(string sign)
        {
            GepgBillMessage gepgBill = new GepgBillMessage() { gepgBillSubReq = newBill, gepgSignature = sign };

            XmlSerializer xs = null;
            XmlSerializerNamespaces ns = null;
            XmlWriterSettings settings = null;
            XmlWriter xw = null;
            String outString = String.Empty;

            try
            {
                ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                settings = new XmlWriterSettings();
                //settings.Indent = true;
                StringBuilder sb = new StringBuilder();
                xs = new XmlSerializer(typeof(GepgBillMessage));
                xw = XmlWriter.Create(sb, settings);

                xs.Serialize(xw, gepgBill, ns);
                xw.Flush();
                outString = sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                if (xw != null)
                {
                    xw.Close();
                }
            }

            return outString;
        }

        public string FinaliseSignedMsg(object content, Type type)
        {
            //Gepg gepgBill = new Gepg() { Content = content, gepgSignature = sign };

            XmlSerializer xs = null;
            XmlSerializerNamespaces ns = null;
            XmlWriterSettings settings = null;
            XmlWriter xw = null;
            String outString = String.Empty;

            try
            {


                ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                settings = new XmlWriterSettings();

                //settings.Indent = true;
                StringBuilder sb = new StringBuilder();
                xs = new XmlSerializer(type);
                xw = XmlWriter.Create(sb, settings);

                xs.Serialize(xw, content, ns);
                xw.Flush();
                outString = sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                if (xw != null)
                {
                    xw.Close();
                }
            }

            return outString;
        }

        public string FinaliseSignedAcks(object content, Type type)
        {
            //Gepg gepgBill = new Gepg() { Content = content, gepgSignature = sign };

            XmlSerializer xs = null;
            XmlSerializerNamespaces ns = null;
            XmlWriterSettings settings = null;
            XmlWriter xw = null;
            String outString = String.Empty;

            try
            {
                ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                settings = new XmlWriterSettings();
                settings.Encoding = System.Text.Encoding.UTF8;

                //settings.Indent = true;
                MemoryStream sb = new MemoryStream();
                xs = new XmlSerializer(type);
                xw = XmlWriter.Create(sb, settings);

                xs.Serialize(xw, content, ns);
                xw.Flush();
                outString = Encoding.UTF8.GetString(sb.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                if (xw != null)
                {
                    xw.Close();
                }
            }

            return outString;
        }

        public string SerializeClean(object bill, Type type)
        {
            XmlSerializer xs = null;
            //These are the objects that will free us from extraneous markup.
            XmlWriterSettings settings = null;
            XmlSerializerNamespaces ns = null;

            //We use a XmlWriter instead of a StringWriter.
            XmlWriter xw = null;

            String outString = String.Empty;

            try
            {
                //To get rid of the xml declaration we create an 
                //XmlWriterSettings object and tell it to OmitXmlDeclaration.
                settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;

                //To get rid of the default namespaces we create a new
                //set of namespaces with one empty entry.
                ns = new XmlSerializerNamespaces();
                ns.Add("", "");

                StringBuilder sb = new StringBuilder();

                xs = new XmlSerializer(type);

                //We create a new XmlWriter with the previously created settings 
                //(to OmitXmlDeclaration).
                xw = XmlWriter.Create(sb, settings);

                //We call xs.Serialize and pass in our custom 
                //XmlSerializerNamespaces object.
                xs.Serialize(xw, bill, ns);

                xw.Flush();

                outString = sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                if (xw != null)
                {
                    xw.Close();
                }
            }
            return outString;
        }

        public string GenerateSignature(string strUnsignedContent)
        {
            string signature = string.Empty;
            try
            {
                byte[] bts = File.ReadAllBytes(PrivateStorePath);
                X509Certificate2 certificate = new X509Certificate2(PrivateStorePath, CertPass);

                rsaCrypto = (RSA)certificate.PrivateKey;

                if (rsaCrypto == null)
                {

                }
                else
                {
                    SHA1Managed sha1 = new SHA1Managed();
                    byte[] hash;

                    hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(strUnsignedContent));

                    byte[] signedHash = rsaCrypto.SignHash(hash, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                    string signedHashString = Convert.ToBase64String(signedHash);
                    signature = signedHashString;
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            return signature;
        }

        public async Task<string> SendHttpRequest(string FunctionURL, string Content, string SPCode, string GepgCom)
        {

            try
            {
                var url = configuration["PaymentGateWay:GePG:Url"];

                // Create a request using a URL that can receive a post.   
                WebRequest request = WebRequest.Create(url + FunctionURL);
                // Set the Method property of the request to POST.  
                request.Method = "POST";
                // Create POST data and convert it to a byte array.  
                byte[] byteArray = Encoding.UTF8.GetBytes(Content);
                // Set the ContentType property of the WebRequest.  
                request.ContentType = "application/xml";
                // Set the ContentLength property of the WebRequest. 
                request.ContentLength = byteArray.Length;
                //Set Custom Headers
                request.Headers.Add("Gepg-Code", SPCode);
                request.Headers.Add("Gepg-Com", GepgCom);
                // Get the request stream.  
                Stream dataStream = request.GetRequestStream();
                // Write the data to the request stream.  
                dataStream.Write(byteArray, 0, byteArray.Length);
                // Close the Stream object.  
                dataStream.Close();


                // Get the response.  
                WebResponse response = await request.GetResponseAsync();
                // Display the status.  
                Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                // Get the stream containing content returned by the server.  
                dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.  
                StreamReader reader = new StreamReader(dataStream);
                // Read the content.  
                string responseFromServer = reader.ReadToEnd();
                // Display the content.  
                Console.WriteLine(responseFromServer);
                // Clean up the streams.  
                reader.Close();
                dataStream.Close();
                response.Close();
                return responseFromServer;
            }
            catch (Exception ex)
            {
                // MessageBox.Show(ex.Message);
                return ex.Message;
            }

        }

        public string GetAccountCodeByPaymentId(int PaymentId)
        {
            var getAccountCodeQuery = @"SELECT DISTINCT AccCodePremiums
                                          FROM tblProduct p
                                          inner join tblPaymentDetails pd on p.ProductCode=pd.ProductCode
                                          where p.ValidityTo is null and pd.PaymentID=@PaymentId";
            SqlParameter[] sqlParameters = {
                        new SqlParameter("@PaymentId", PaymentId),
                };
            var sData = new DataHelper(configuration);
            string accountCode = "";
            DataTable results = sData.GetDataTable(getAccountCodeQuery, sqlParameters, CommandType.Text);
            if (results.Rows.Count > 0)
            {
                var result = results.Rows[0];
                if (!string.IsNullOrEmpty(Convert.ToString(result["AccCodePremiums"])))
                {
                    accountCode = Convert.ToString(result["AccCodePremiums"]);
                }
            }
            return accountCode;
        }

        public string GetAccountCodeByProductCode(string ProductCode)
        {
            var getAccountCodeQuery = @"SELECT AccCodePremiums FROM tblProduct WHERE ProductCode = @ProductCode AND ValidityTo is NULL";
            SqlParameter[] sqlParameters = {
                        new SqlParameter("@ProductCode", ProductCode),
                };
            var sData = new DataHelper(configuration);
            string accountCode = "";
            DataTable results = sData.GetDataTable(getAccountCodeQuery, sqlParameters, CommandType.Text);
            if (results.Rows.Count > 0)
            {
                var result = results.Rows[0];
                if (!string.IsNullOrEmpty(Convert.ToString(result["AccCodePremiums"])))
                {
                    accountCode = Convert.ToString(result["AccCodePremiums"]);
                }
            }
            return accountCode;
        }

    }
}
