using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff;

namespace M225dwScan.Models
{
    public class ScanService
    {
        private string scanImageProgPath = "scanimage";
        public ScanService(IWebHostEnvironment env)
        {
            var simulatorPath = Directory.GetFiles(env.ContentRootPath, "ScanImageSimulator*").FirstOrDefault();
            if (simulatorPath != null) scanImageProgPath = simulatorPath;
        }

        public async Task<byte[]> Scan(string mode, int resolution, string source,
            int top, int left, int width, int height,
            Action<string> progressCallback)
        {
            var si = new ProcessStartInfo()
            {
                FileName = scanImageProgPath,
                Arguments = $"-v -p --format tiff -d \"brother4:net1;dev0\" -l {left} -t {top} -x {width} -y {height} --source \"{source}\" --resolution {resolution} --mode \"{mode}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var p = new Process())
            {
                var progress = false;
                p.ErrorDataReceived += new DataReceivedEventHandler(
                    (sender, e) =>
                    {
                        var s = e.Data;
                        if (!string.IsNullOrEmpty(s))
                        {
                            if (s.Contains("%"))
                            {
                                progressCallback("\r" + s);
                                progress = true;
                            }
                            else
                            {
                                if (progress) progress = false;
                                progressCallback(s);
                            }
                        }
                    });
                p.StartInfo = si;
                p.Start();
                p.BeginErrorReadLine();
                var img = default(byte[]);
                using (var ms = new MemoryStream())
                {
                    byte[] buff = new byte[8192];
                    int len = 0;
                    do
                    {
                        len = p.StandardOutput.BaseStream.Read(buff, 0, buff.Length);
                        if (len > 0) ms.Write(buff, 0, len);
                    } while (len > 0);
                    var tiff = Image.Load(ms.ToArray(), new TiffDecoder());
                    using (var conv = new MemoryStream()) {
                        tiff.Save(conv, new JpegEncoder());
                        img = conv.ToArray();
                    }
                }               
                await p.WaitForExitAsync();
                return img;
            }

        }

    }
}