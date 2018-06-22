﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImisRestApi.Controllers
{
    [Authorize(Roles = "IMISAdmin")]
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        // GET api/values
        [Authorize(Roles = "EnrollmentOfficer")]
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2", User.Identity.Name };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
