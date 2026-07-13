using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OSDeploymentAssistant.Integrations
{
    /// <summary>
    /// Talks to a ServiceNow instance using the current Windows session
    /// (UseDefaultCredentials = true → NTLM/Kerberos passthrough).
    ///
    /// IMPORTANT CAVEAT:
    /// ServiceNow itself does not natively speak NTLM. This only works if one of
    /// the following is true for your instance:
    ///   - Integrated Windows Auth is fronted by an IIS reverse proxy / ADFS
    ///     that terminates NTLM/Kerberos and forwards an authenticated session, OR
    ///   - Your ServiceNow instance is configured for SSO (SAML/ADFS) and the
    ///     machine already holds a valid session/cookie from a prior browser login.
    /// If neither is true, you'll get a 401 and will need to fall back to
    /// Basic Auth or an OAuth token instead (talk to your ServiceNow admin to confirm).
    /// </summary>
    public class ServiceNowClient
    {
        private readonly HttpClient _http;

        public ServiceNowClient(string instanceBaseUrl)
        {
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true, // pass the logged-in Windows identity through
                PreAuthenticate = true
            };

            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(instanceBaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Directly creates ONE sc_req_item (RITM) record for an SCCM asset request.
        /// This is a straight Table API insert — not a generic "order_now" catalog
        /// checkout — so it skips catalog variable/pricing logic and just opens the
        /// ticket record itself, the same way you'd open an SCCM asset entry.
        /// </summary>
        public async Task<ServiceNowTicketResult> CreateSccmAssetRequestAsync(ServiceNowRequestItem item)
        {
            if (string.IsNullOrWhiteSpace(item.CatalogItemSysId))
                throw new ServiceNowException("CatalogItemSysId is not configured. Set SccmCatalogItemSysId in MainWindow.xaml.cs.");

            var payload = new
            {
                cat_item = item.CatalogItemSysId,
                short_description = item.ShortDescription,
                requested_for = item.RequestedFor,
                description =
                    $"Asset Name: {item.AssetName}\n" +
                    $"MAC Address: {item.MacAddress}\n" +
                    $"Node: {item.Node}\n" +
                    $"Operating System: {item.OperatingSystem}"
                // Add any other sc_req_item fields your instance requires here,
                // e.g. "assignment_group", "priority", or custom fields like "u_asset_name".
            };

            const string endpoint = "/api/now/table/sc_req_item";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync(endpoint, content);
            }
            catch (Exception ex)
            {
                throw new ServiceNowException($"Network error contacting ServiceNow: {ex.Message}", ex);
            }

            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ServiceNowException($"ServiceNow returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");

            using var doc = JsonDocument.Parse(body);
            var result = doc.RootElement.GetProperty("result");

            return new ServiceNowTicketResult
            {
                SysId = result.TryGetProperty("sys_id", out var sysIdEl) ? sysIdEl.GetString() ?? "" : "",
                Number = result.TryGetProperty("number", out var numEl) ? numEl.GetString() ?? "" : ""
            };
        }
    }
}