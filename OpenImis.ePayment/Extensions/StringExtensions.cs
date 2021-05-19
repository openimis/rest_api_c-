﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenImis.ePayment.Extensions
{
    public static class StringExtensions
    {
        public static int? GetErrorNumber(this string str)
        {
            if (String.IsNullOrEmpty(str))
            {
                return null;
            }
            else
            {
                try
                {
                    var error = str.Split(":");
                    var errorNumber = int.Parse(error[0]);
                    return errorNumber;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static string GetErrorMessage(this string str)
        {
            if (String.IsNullOrEmpty(str))
            {
                return str;
            }
            else
            {              
                try
                {
                    var error = str.Split(":");
                    int.Parse(error[0]);
                    var errorMessae = error[1];
                    return errorMessae;
                }
                catch (Exception)
                {

                    return str;
                }
            }
        }
    }
}
