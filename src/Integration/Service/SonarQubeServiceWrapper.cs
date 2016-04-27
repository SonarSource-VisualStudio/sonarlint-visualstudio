//-----------------------------------------------------------------------
// <copyright file="SonarQubeServiceWrapper.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Integration.Service.DataModel;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SonarLint.VisualStudio.Integration.Service
{
    /// <summary>
    /// SonarQube service wrapper.
    /// The class is not thread safe.
    /// </summary>
    [Export(typeof(SonarQubeServiceWrapper)), PartCreationPolicy(CreationPolicy.Shared)]
    internal class SonarQubeServiceWrapper : ISonarQubeServiceWrapper
    {
        public const string ProjectsAPI               = "api/projects/index";                 // Since 2.10
        public const string ServerPluginsInstalledAPI = "api/updatecenter/installed_plugins"; // Since 2.10; internal
        public const string QualityProfileListAPI     = "api/profiles/list";                  // Since 3.3; deprecated in 5.2
        public const string QualityProfileExportAPI   = "profiles/export";                    // Since ???; internal
        public const string PropertiesAPI             = "api/properties/";                    // Since 2.6

        public const string ProjectDashboardRelativeUrl = "dashboard/index/{0}";

        public const string RoslynExporter = "roslyn-cs";

        public const string CSharpLanguage = "cs";
        public const string VBLanguage = "vbnet";
        public static readonly string[] SupportedLanguages = { CSharpLanguage, VBLanguage };

        private readonly IServiceProvider serviceProvider;
        private readonly TimeSpan requestTimeout;

        #region Constructors

        [ImportingConstructor]
        public SonarQubeServiceWrapper([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
            : this(serviceProvider, TimeSpan.FromSeconds(100) /*The default value for HttpClient*/)
        {
        }

        protected SonarQubeServiceWrapper(IServiceProvider serviceProvider, TimeSpan timeout)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            this.serviceProvider = serviceProvider;

            // No need to validate, HttpClient will do it anyway.
            this.requestTimeout = timeout;
        }

        #endregion

        #region ISonarQubeServiceWrapper

        public bool TryGetProjects(ConnectionInformation connectionInformation, CancellationToken token, out ProjectInformation[] serverProjects)
        {
            if (connectionInformation == null)
            {
                throw new ArgumentNullException(nameof(connectionInformation));
            }

            serverProjects = this.SafeUseHttpClient<ProjectInformation[]>(connectionInformation,
                client => this.DownloadProjects(client, token));

            return serverProjects != null;
        }

        public bool TryGetProperties(ConnectionInformation connectionInformation, CancellationToken token, out ServerProperty[] properties)
        {
            if (connectionInformation == null)
            {
                throw new ArgumentNullException(nameof(connectionInformation));
            }

            properties = this.SafeUseHttpClient<ServerProperty[]>(connectionInformation,
                client => GetServerProperties(client, token));

            return properties != null;
        }


        public bool TryGetExportProfile(ConnectionInformation connectionInformation, ProjectInformation project, string language, CancellationToken token, out RoslynExportProfile profile)
        {
            if (connectionInformation == null)
            {
                throw new ArgumentNullException(nameof(connectionInformation));
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (!SupportedLanguages.Contains(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }


            profile = this.SafeUseHttpClient<RoslynExportProfile>(connectionInformation,
                client => this.DownloadExportForProject(client, project, language, token));

            return profile != null;
        }

        public bool TryGetPlugins(ConnectionInformation connectionInformation, CancellationToken token, out ServerPlugin[] plugins)
        {
            if (connectionInformation == null)
            {
                throw new ArgumentNullException(nameof(connectionInformation));
            }

            plugins = this.SafeUseHttpClient<ServerPlugin[]>(connectionInformation,
                client => DownloadPluginInformation(client, token));

            return plugins != null;
        }

        public Uri CreateProjectDashboardUrl(ConnectionInformation connectionInformation, ProjectInformation project)
        {
            if (connectionInformation == null)
            {
                throw new ArgumentNullException(nameof(connectionInformation));
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return new Uri(connectionInformation.ServerUri, string.Format(ProjectDashboardRelativeUrl, project.Key));
        }

        #endregion

        internal /*for testing purposes*/ protected virtual HttpClient CreateHttpClient()
        {
            return new HttpClient();
        }

        #region Download projects

        private async Task<ProjectInformation[]> DownloadProjects(HttpClient configuredClient, CancellationToken token)
        {
            HttpResponseMessage downloadProjectsResponse = await InvokeGetRequest(configuredClient, ProjectsAPI, token);
            return await ProcessJsonResponse<ProjectInformation[]>(downloadProjectsResponse, token);
        }

        #endregion

        #region Server plugin info

        private static async Task<ServerPlugin[]> DownloadPluginInformation(HttpClient client, CancellationToken token)
        {
            string getPluginsUrl = ServerPluginsInstalledAPI;
            HttpResponseMessage response = await InvokeGetRequest(client, getPluginsUrl, token);

            return await ProcessJsonResponse<ServerPlugin[]>(response, token);
        }

        #endregion

        #region Quality profile

        internal /*for testing purposes*/ static string CreateQualityProfileUrl(string language, ProjectInformation project = null)
        {
            if (project == null)
            {
                return AppendQueryString(QualityProfileListAPI, "?language={0}", language);
            }
            else
            {
                return AppendQueryString(QualityProfileListAPI, "?language={0}&project={1}", language, project.Key);
            }
        }

        internal /*for testing purposes*/ static async Task<QualityProfile> DownloadQualityProfile(HttpClient client, ProjectInformation project, string language, CancellationToken token)
        {
            string apiUrl = CreateQualityProfileUrl(language, project);
            HttpResponseMessage response = await InvokeGetRequest(client, apiUrl, token, ensureSuccess: false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Special handling for the case when a project was not analyzed yet, in which case a 404 is returned
                // Request the profile without the project
                bool ensureSuccess = true;
                apiUrl = CreateQualityProfileUrl(language);
                response = await InvokeGetRequest(client, apiUrl, token, ensureSuccess);
            }

            response.EnsureSuccessStatusCode(); // Bubble up the rest of the errors

            QualityProfile[] profiles = await ProcessJsonResponse<QualityProfile[]>(response, token);

            return profiles.Length > 1
                ? profiles.Single(x => x.IsDefault)
                : profiles.Single();
        }

        #endregion

        #region Roslyn export profile

        internal /*for testing purposes*/ static string CreateQualityProfileExportUrl(QualityProfile profile, string language, string exporter)
        {
            return AppendQueryString(QualityProfileExportAPI, "?name={0}&language={1}&format={2}", profile.Name, language, exporter);
        }

        private async Task<RoslynExportProfile> DownloadExportForProject(HttpClient client, ProjectInformation project, string language, CancellationToken token)
        {
            RoslynExportProfile export = null;
            var profile = await DownloadQualityProfile(client, project, language, token);
            if (profile != null)
            {
                export = await DownloadQualityProfileExport(client, profile, language, token);
            }

            return export;
        }

        private static async Task<RoslynExportProfile> DownloadQualityProfileExport(HttpClient client, QualityProfile profile, string language, CancellationToken token)
        {
            string api = CreateQualityProfileExportUrl(profile, language, RoslynExporter);
            HttpResponseMessage response = await InvokeGetRequest(client, api, token);

            using (Stream stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                return RoslynExportProfile.Load(reader);
            }
        }

        #endregion

        #region Server properties

        private static async Task<ServerProperty[]> GetServerProperties(HttpClient client, CancellationToken token)
        {
            HttpResponseMessage response = await InvokeGetRequest(client, PropertiesAPI, token);

            return await ProcessJsonResponse<ServerProperty[]>(response, token);
        }

        #endregion

        #region Helpers

        internal /*for testing purposes*/ static string AppendQueryString(string urlBase, string queryFormat, params string[] args)
        {
            return urlBase + string.Format(CultureInfo.InvariantCulture, queryFormat, args.Select(HttpUtility.UrlEncode).ToArray());
        }

        internal /*for testing purposes*/ static Uri CreateRequestUrl(HttpClient client, string apiUrl)
        {
            // We normalise these inputs to that the client base address always has a trailing slash,
            // and the API url has no leading slash.
            // Failure to do so will cause an incorrect URL to be formed, with the apiUrl being relative
            // to the base address's hostname only.
            Debug.Assert(client.BaseAddress.ToString().EndsWith("/"), "HttpClient.BaseAddress should have a trailing slash");
            Debug.Assert(!apiUrl.StartsWith("/"), "API URLs should not begin with a slash as this forces the API to be relative to the base URI's hostname.");

            Uri normalBaseUri = client.BaseAddress.EnsureTrailingSlash();
            string normalApiUrl = apiUrl.TrimStart('/');

            return new Uri(normalBaseUri, normalApiUrl);
        }

        private static async Task<HttpResponseMessage> InvokeGetRequest(HttpClient client, string apiUrl, CancellationToken token, bool ensureSuccess = true)
        {
            var uri = CreateRequestUrl(client, apiUrl);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

            if (ensureSuccess)
            {
                response.EnsureSuccessStatusCode();
            }

            return response;
        }

        private static async Task<T> ProcessJsonResponse<T>(HttpResponseMessage response, CancellationToken token)
        {
            Debug.Assert(response != null && response.IsSuccessStatusCode, "Invalid response");

            token.ThrowIfCancellationRequested();
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            token.ThrowIfCancellationRequested();
            return JsonHelper.Deserialize<T>(content);
        }

        private T SafeUseHttpClient<T>(ConnectionInformation connectionInformation, Func<HttpClient, Task<T>> sendRequestsAsync)
        {
            using (HttpClient client = this.CreateHttpClient())
            {
                client.BaseAddress = connectionInformation.ServerUri;
                client.DefaultRequestHeaders.Authorization = AuthenticationHeaderProvider.GetAuthenticationHeader(connectionInformation);
                client.Timeout = this.requestTimeout;
                T result = default(T);

                try
                {
                    result = sendRequestsAsync(client).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (HttpRequestException e)
                {
                    // For some errors we will get an inner exception which will have a more specific information
                    // that we would like to show i.e.when the host could not be resolved
                    System.Net.WebException innerException = e.InnerException as System.Net.WebException;
                    VsShellUtils.WriteToSonarLintOutputPane(this.serviceProvider, Strings.SonarQubeRequestFailed, e.Message, innerException?.Message);
                }
                catch (TaskCanceledException)
                {
                    // Cancelled or timeout
                    VsShellUtils.WriteToSonarLintOutputPane(this.serviceProvider, Strings.SonarQubeRequestTimeoutOrCancelled);
                }
                catch (Exception ex)
                {
                    if (ErrorHandler.IsCriticalException(ex))
                    {
                        throw;
                    }

                    VsShellUtils.WriteToSonarLintOutputPane(this.serviceProvider, Strings.SonarQubeRequestFailed, ex.Message, null);
                }
                
                return result;
            }
        }

        #endregion
    }
}
