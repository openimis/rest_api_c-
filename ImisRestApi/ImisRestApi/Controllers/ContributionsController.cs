﻿using ImisRestApi.Data;
using ImisRestApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace ImisRestApi.Controllers
{
    [Authorize]
    public class ContributionsController : Controller
    {
        private ImisContribution contribution;

        public ContributionsController(IConfiguration configuration)
        {
            contribution = new ImisContribution(configuration);
        }

        [HttpPost]
        [Route("api/Contributions/Enter_Contribution")]
        public IActionResult Enter_Contribution([FromBody]Contribution model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = contribution.Enter(model);

            return Json(response);

            
        }

    }
}
