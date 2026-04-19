using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using WileyCoWeb.Components;
using WileyCoWeb.Services;
using WileyCoWeb.State;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace WileyCoWeb
{
    public static class Program
    {
        public static Task Main(string[] args) => ClientStartup.RunAsync(args);
    }
}
