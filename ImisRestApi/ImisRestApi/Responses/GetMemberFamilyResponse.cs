﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace ImisRestApi.Responses
{
    public class GetMemberFamilyResponse : ImisApiResponse
    {
        public GetMemberFamilyResponse(Exception e):base(e)
        {

        }
        public GetMemberFamilyResponse(int value,bool error):base(value,error)
        {
            SetMessage(value);
        }
        public GetMemberFamilyResponse(int value, bool error,DataTable data) : base(value, error,data)
        {
            SetMessage(value);
            
        }

        private void SetMessage(int value)
        {
            switch (value)
            {
                case 0:              
                    msg.Code = value;
                    msg.MessageValue = "Success.";
                    Message = msg;
                    break;
                case 1:

                    msg.Code = value;
                    msg.MessageValue = "Wrong Format or Missing Insurance Number of head";
                    Message = msg;
                    break;
                case 2:
                    msg.Code = value;
                    msg.MessageValue = "Insurance number of head not found";
                    Message = msg;
                    break;
                case 3:                 
                    msg.Code = value;
                    msg.MessageValue = "No member of the specified order number in the family/group";
                    Message = msg;
                    break;
            }
        }
    }
}