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
            var body = Response.Body;
            Action<string, bool> print = async (msg, padding) =>
            {
                if (padding) msg = msg.PadRight(1024, ' ');
                var data = System.Text.Encoding.UTF8.GetBytes(msg);
                await body.WriteAsync(data, 0, data.Length);
                await body.FlushAsync();
            };

            print(@"
<html>
<head>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <style>
        html,body{font-size:9pt;}
        button { width: 250px; padding: 12px 6px; font-size: 12pt; margin: 3px; }
    </style>
</head>
<body></body>
<script>
function updateProgress(msg) {
    var p = document.getElementById('progress');
    if (!p) {
        p = document.createElement('div');
        p.setAttribute('id','progress');
        document.body.appendChild(p);
    }
    p.innerText = msg;
}
function printMessage(msg) {
    var m = document.createElement('div');
    m.innerText = msg;
    document.body.appendChild(m);
}
var hnd;
function disableDownloadButton() {
    clearInterval(hnd);
    document.getElementById('dlbtn').disabled = true;
}
function download(token) {
    disableDownloadButton();
    var f = document.createElement('iframe');
    f.src = '/Scan/Download/' + token;
    f.style.display = 'none';
    document.body.appendChild(f);
}
function countdown() {
    let s = 30;
    hnd = setInterval(function() {
        document.getElementById('cd').innerText = s--;
        if (s <= 0) disableDownloadButton();
    }, 1000);
}
</script>", false);
            Func<object, string> toJson = o => System.Text.Json.JsonSerializer.Serialize(o);
            var img = await _scanSvc.Scan(mode, resolution, source, top, left, width, height,
                (msg) =>
                {
                    if (msg.StartsWith('\r'))
                    {
                        print($"<script>updateProgress({toJson(msg.TrimStart('\r'))});</script>", true);
                    }
                    else print($"<script>printMessage({toJson(msg)});</script>", true);
                });
            var token = Guid.NewGuid().ToString();
            _cache.Set<byte[]>(token, img, new MemoryCacheEntryOptions().SetAbsoluteExpiration(DateTime.Now.AddSeconds(30)));
            print(@$"<div>
            <button onclick=""download('{token}')"" id=dlbtn><span>Download Image</span> (<span id='cd'>30</span>s)</button>
            </div>
            <script>countdown();</script>
            <div><button onclick=""location.href='/'"">Back</button></div>", false);
            print("</html>", false);
        }

        public IActionResult Download(string id)
        {
            var img = _cache.Get<byte[]>(id);
            return File(img, "image/jpeg", $"{DateTime.Now:yyyyMMddHHmmss}.jpg");
        }
    }
}