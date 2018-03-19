﻿using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TickTrader.BotAgent.BA;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using TickTrader.BotAgent.BA.Exceptions;
using TickTrader.BotAgent.WebAdmin.Server.Extensions;
using TickTrader.BotAgent.WebAdmin.Server.Dto;
using TickTrader.BotAgent.BA.Models;
using System.Net;
using TickTrader.BotAgent.BA.Entities;

namespace TickTrader.BotAgent.WebAdmin.Server.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class TradeBotsController : Controller
    {
        private readonly ILogger<TradeBotsController> _logger;
        private readonly IBotAgent _botAgent;

        public TradeBotsController(IBotAgent ddServer, ILogger<TradeBotsController> logger)
        {
            _botAgent = ddServer;
            _logger = logger;
        }

        [HttpGet]
        public TradeBotDto[] Get()
        {
            var bots = _botAgent.GetTradeBots();
            return bots.Select(b => b.ToDto()).ToArray();
        }

        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            try
            {
                var tradeBot = _botAgent.GetBotInfo(WebUtility.UrlDecode(id));

                return Ok(tradeBot.ToDto());
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        #region Logs
        [HttpDelete("{id}/Logs")]
        public IActionResult DeleteLogs(string id)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);
                var log = _botAgent.GetBotLog(botId);
                log.Clear();

                return Ok();
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpGet("{id}/[Action]")]
        public IActionResult Logs(string id)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);
                var log = _botAgent.GetBotLog(botId);

                return Ok(log.ToDto());
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpGet("{id}/[Action]/{file}")]
        public IActionResult Logs(string id, string file)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);
                var log = _botAgent.GetBotLog(botId);

                var decodedFile = WebUtility.UrlDecode(file);
                var readOnlyFile = log.GetFile(decodedFile);

                return File(readOnlyFile.OpenRead(), MimeMipping.GetContentType(decodedFile), decodedFile);
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpDelete("{id}/Logs/{file}")]
        public IActionResult DeleteLog(string id, string file)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);
                var log = _botAgent.GetBotLog(botId);
                log.DeleteFile(WebUtility.UrlDecode(file));

                return Ok();
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }
        #endregion

        #region AlgoData
        [HttpGet("{id}/[Action]")]
        public IActionResult AlgoData(string id)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);
                var algoData = _botAgent.GetAlgoData(botId);

                var files = algoData.Files.Select(f => f.ToDto()).ToArray();

                return Ok(files);
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpGet("{id}/[Action]/{file}")]
        public IActionResult AlgoData(string id, string file)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);
                var algoData = _botAgent.GetAlgoData(botId);

                var decodedFile = WebUtility.UrlDecode(file);
                var readOnlyFile = algoData.GetFile(decodedFile);

                return File(readOnlyFile.OpenRead(), MimeMipping.GetContentType(decodedFile), decodedFile);
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }
        #endregion

        [HttpGet("{id}/[Action]")]
        public IActionResult Status(string id)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);
                var log = _botAgent.GetBotLog(botId);

                return Ok(new BotStatusDto
                {
                    Status = log.Status,
                    BotId = botId
                });
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpGet("{botName}/[action]")]
        public string BotId(string botName)
        {
            return _botAgent.GenerateBotId(WebUtility.UrlDecode(botName));
        }

        [HttpPost]
        public IActionResult Post([FromBody]PluginSetupDto setup)
        {
            try
            {
                var pluginCfg = setup.Parse();
                var accountKey = new AccountKey(setup.Account.Login, setup.Account.Server);

                var config = new TradeBotConfig
                {
                    Plugin = new PluginKey(setup.PackageName, setup.PluginId),
                    PluginConfig = pluginCfg,
                };

                var tradeBot = _botAgent.AddBot(accountKey, config);
                setup.EnsureFiles(ServerModel.GetWorkingFolderFor(tradeBot.Id));

                return Ok(tradeBot.ToDto());
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpPut("{id}")]
        public IActionResult Put(string id, [FromBody]PluginSetupDto setup)
        {
            try
            {
                var botId = WebUtility.UrlDecode(id);

                var pluginCfg = setup.Parse();
                var config = new TradeBotConfig
                {
                    PluginConfig = pluginCfg,
                };
                config.PluginConfig.InstanceId = botId;

                _botAgent.ChangeBotConfig(botId, config);
                setup.EnsureFiles(ServerModel.GetWorkingFolderFor(botId));

                return Ok();
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(string id, [FromQuery] bool clean_log = true, [FromQuery] bool clean_algodata = true)
        {
            try
            {
                _botAgent.RemoveBot(WebUtility.UrlDecode(id), clean_log, clean_algodata);

                return Ok();
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpPatch("{id}/[action]")]
        public IActionResult Start(string id)
        {
            try
            {
                _botAgent.StartBot(WebUtility.UrlDecode(id));

                return Ok();
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }

        [HttpPatch("{id}/[action]")]
        public IActionResult Stop(string id)
        {
            try
            {
                _botAgent.StopBotAsync(WebUtility.UrlDecode(id));

                return Ok();
            }
            catch (BAException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.ToBadResult());
            }
        }
    }
}
