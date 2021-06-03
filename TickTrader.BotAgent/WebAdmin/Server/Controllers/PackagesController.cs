﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TickTrader.Algo.Domain;
using TickTrader.Algo.Domain.ServerControl;
using TickTrader.BotAgent.BA;
using TickTrader.BotAgent.WebAdmin.Server.Dto;
using TickTrader.BotAgent.WebAdmin.Server.Extensions;

namespace TickTrader.BotAgent.WebAdmin.Server.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class PackagesController : Controller
    {
        private readonly ILogger<PackagesController> _logger;
        private readonly IBotAgent _botAgent;

        public PackagesController(IBotAgent ddServer, ILogger<PackagesController> logger)
        {
            _botAgent = ddServer;
            _logger = logger;
        }

        [HttpGet]
        public async Task<PackageDto[]> Get()
        {
            var packages = await _botAgent.GetPackageSnapshot();

            return packages.Select(p => p.ToDto()).ToArray();
        }

        [HttpHead("{name}")]
        public async Task<IActionResult> Head(string pkgName)
        {
            if (await _botAgent.PackageWithNameExists(pkgName))
                return Ok();

            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Post(IFormFile file)
        {
            if (file == null) throw new ArgumentNullException("File is null");
            if (file.Length == 0) throw new ArgumentException("File is empty");

            try
            {
                var tmpFile = Path.GetTempFileName();
                using (var fileStream = System.IO.File.OpenWrite(tmpFile))
                {
                    await file.CopyToAsync(fileStream);
                }

                await _botAgent.UploadPackage(new UploadPackageRequest(null, file.FileName), tmpFile);

                if (System.IO.File.Exists(tmpFile))
                    System.IO.File.Delete(tmpFile);
            }
            catch (AlgoException algoEx)
            {
                _logger.LogError(algoEx.Message);
                return BadRequest(algoEx.ToBadResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Package upload failed");
            }

            return Ok();
        }

        [HttpDelete("{name}")]
        public async Task<IActionResult> Delete(string packageId)
        {
            try
            {
                await _botAgent.RemovePackage(new RemovePackageRequest(WebUtility.UrlDecode(packageId)));
            }
            catch (AlgoException algoEx)
            {
                _logger.LogError(algoEx.Message);
                return BadRequest(algoEx.ToBadResult());
            }

            return Ok();
        }
    }
}
