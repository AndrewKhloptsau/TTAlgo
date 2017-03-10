﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using TickTrader.DedicatedServer.DS;
using TickTrader.DedicatedServer.DS.Models.Exceptions;
using TickTrader.DedicatedServer.WebAdmin.Server.Dto;
using TickTrader.DedicatedServer.WebAdmin.Server.Extensions;

namespace TickTrader.DedicatedServer.WebAdmin.Server.Controllers
{
    [Route("api/[controller]")]
    public class AccountController : Controller
    {
        private readonly ILogger<RepositoryController> _logger;
        private readonly IDedicatedServer _dedicatedServer;
        
        public AccountController(IDedicatedServer ddServer, ILogger<RepositoryController> logger)
        {
            _dedicatedServer = ddServer;
            _logger = logger;
        }

        [HttpGet]
        public AccountDto[] Get()
        {
            return _dedicatedServer.Accounts.Select(a => a.ToDto()).ToArray();
        }

        [HttpPost]
        public IActionResult Post([FromBody]AccountDto account)
        {
            try
            {
                _dedicatedServer.AddAccount(account.Login, account.Password, account.Server);
            }
            catch(DuplicateAccountException dae)
            {
                _logger.LogError(dae.Message);
                return BadRequest(new { Code = dae.Code, Message = dae.Message });
            }

            return Ok();
        }

        [HttpDelete]
        public void Delete(string login, string server)
        {
            _dedicatedServer.RemoveAccount(login ?? "", server ?? "");
        }

       
    }
}
