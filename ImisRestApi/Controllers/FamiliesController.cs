﻿using ImisRestApi.Chanels.Sms;
using ImisRestApi.Data;
using ImisRestApi.Logic;
using ImisRestApi.Models;
using ImisRestApi.Models.Sms;
using ImisRestApi.Responses;
using ImisRestApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ImisRestApi.Controllers
{
    [Authorize]
    public class FamiliesController : Controller
    {
        private ImisFamily family;
        private IConfiguration config;
        public FamiliesController(IConfiguration configuration)
        {
            config = configuration;
            family = new ImisFamily(configuration);
        }
        // GET api/Families
        [HasRights(Rights.FamilySearch)]
        [HttpGet]
        [Route("api/Families/Get_Family")]
        public IActionResult Get(string insureeNumber)
        {

            DataMessage response;
            try
            {
                if (insureeNumber != null || insureeNumber.Length != 0)
                {
                    var data = family.Get(insureeNumber);

                    if (data.Rows.Count > 0)
                    {
                        response = new GetFamilyResponse(0, false, data,0).Message;

                    }
                    else
                    {
                        response = new GetFamilyResponse(2, false,0).Message;
                    }
                }
                else
                {
                    response = new GetFamilyResponse(1, true,0).Message;
                }
                
            }
            catch (Exception e)
            {
                response = new GetFamilyResponse(e).Message;
                
            }
            

            return Json(response);
        }

        // GET api/Families/5
        [HasRights(Rights.FamilySearch)]
        [HttpGet]
        [Route("api/Families/Get_Member_Family")]
        public IActionResult Get_Member_Family(string insureeNumber, int order)
        {
           
           
            DataMessage response;
            try
            {
                if (insureeNumber != null || insureeNumber.Length != 0)
                {
                    var data = family.GetMamber(insureeNumber, order);

                    if (data.Rows.Count > 0)
                    {
                        response = new GetMemberFamilyResponse(0, false, data,0).Message;

                    }
                    else
                    {
                        response = new GetMemberFamilyResponse(2, true,0).Message;
                    }
                }
                else
                {
                    response = new GetMemberFamilyResponse(1, true,0).Message;
                }

            }
            catch (Exception e)
            {
                response = new GetMemberFamilyResponse(e).Message;

            }
            return Json(response);
        }

        [HasRights(Rights.FamilyAdd)]
        [HttpPost]
        [Route("api/Families/Enter_Family")]
        public IActionResult Enter_Family([FromBody]Family model)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.FirstOrDefault().Errors.FirstOrDefault().ErrorMessage;
                return BadRequest(new { error_occured = true, error_message = error });
            }

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            family.UserId = Convert.ToInt32(identity.FindFirst("UserId").Value);

            var response = family.AddNew(model);

            return Json(response);

        }

        [HasRights(Security.Rights.FamilyAdd)]
        [HttpPost]
        [Route("api/Families/Enter_Member_Family")]
        public IActionResult Enter_Member_Family([FromBody]FamilyMember model)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.FirstOrDefault().Errors.FirstOrDefault().ErrorMessage;
                return BadRequest(new { error_occured = true, error_message = error });
            }

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            family.UserId = Convert.ToInt32(identity.FindFirst("UserId").Value);

            var response = family.AddMamber(model);

            return Json(response);

        }

        [HasRights(Security.Rights.FamilyEdit)]
        [HttpPost]
        [Route("api/Families/Edit_Family")]
        public IActionResult Edit_Family([FromBody]EditFamily model)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.FirstOrDefault().Errors.FirstOrDefault().ErrorMessage;
                return BadRequest(new { error_occured = true, error_message = error });
            }
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            family.UserId = Convert.ToInt32(identity.FindFirst("UserId").Value);

            var response = family.Edit(model);

            return Json(response);

        }

        [HasRights(Security.Rights.FamilyEdit)]
        [HttpPost]
        [Route("api/Families/Edit_Member_Family")]
        public IActionResult Edit_Member_Family([FromBody]EditFamilyMember model)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.FirstOrDefault().Errors.FirstOrDefault().ErrorMessage;
                return BadRequest(new { error_occured = true, error_message = error });
            }
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            family.UserId = Convert.ToInt32(identity.FindFirst("UserId").Value);

            var response = family.EditMamber(model);

            return Json(response);

        }

        [HasRights(Security.Rights.FamilyDelete)]
        [HttpPost]
        [Route("api/Families/Delete_Member_Family")]
        public IActionResult Delete_Member_Family([FromBody]string insureeNumber)
        {
            if (new ValidationBase().InsureeNumber(insureeNumber) != ValidationResult.Success)
            {
                return BadRequest(new { error_occured = true, error_message = "1:Wrong format or missing insurance number of insuree" });
            }
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            family.UserId = Convert.ToInt32(identity.FindFirst("UserId").Value);

            var response = family.DeleteMamber(insureeNumber);

            return Json(response);

        }
        
    }
}
