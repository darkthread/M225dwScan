using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using M225dwScan.Models;
using Microsoft.Extensions.Caching.Memory;

namespace M225dwScan.Controllers
{
    public class ScanController : Controller
    {
        private readonly ILogger<ScanController> _logger;
        private readonly ScanService _scanSvc;
        private readonly IMemoryCache _cache;

        public ScanController(ILogger<ScanController> logger, ScanService scanSvc, IMemoryCache cache)
        {
            _cache = cache;
            _logger = logger;
            _scanSvc = scanSvc;
        }

        public async Task Index(string mode = "24bit Color[Fast]", int resolution = 200, string source = "FlatBed",
            int top = 0, int left = 0, int width = 100, int height = 100)
        {
            Response.ContentType = "text/event-stream";
            var body = Response.Body;
            var enc = System.Text.Encoding.UTF8;
            Action<string> writeBody = async (s) => {
                var data = enc.GetBytes(s);
                await body.WriteAsync(data, 0, data.Length);
                await body.FlushAsync();
            };
            Action<string, string> printSse = (evtId, msg) =>
            {
                writeBody($"event: {evtId}\n");
                writeBody($"data: {msg}\n\n");
            };
            printSse("verbose", $"開始掃描：{mode}, {resolution} DPI");
            var img = await _scanSvc.Scan(mode, resolution, source, top, left, width, height,
                (msg) =>
                {
                    if (msg.StartsWith('\r'))
                        printSse("progress", msg.TrimStart('\r'));
                    else 
                        printSse("verbose", msg);
                });
            var token = Guid.NewGuid().ToString();
            _cache.Set<byte[]>(token, img, new MemoryCacheEntryOptions().SetAbsoluteExpiration(DateTime.Now.AddSeconds(30)));
            printSse("file", token);
        }

        public IActionResult Download(string id)
        {
            var img = _cache.Get<byte[]>(id);
            return File(img, "image/jpeg", $"{DateTime.Now:yyyyMMddHHmmss}.jpg");
        }
    }
}