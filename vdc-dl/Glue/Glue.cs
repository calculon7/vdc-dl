using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VdcDl;

namespace VdcDl.Glue {
    public class GlueSession : IFtp {
        RestClient client;
        readonly string baseUrlB2;
        readonly string baseUrlB4;
        readonly string baseUrlAccounts;
        GlueCompany connectedHost;

        public GlueSession() {
            this.IsStaging = false;

            this.baseUrlB2 = "b2.autodesk.com";
            this.baseUrlB4 = "b4.autodesk.com";
            this.baseUrlAccounts = "accounts.autodesk.com";
        }

        public GlueSession(bool staging) {
            this.IsStaging = staging;

            if (staging) {
                this.baseUrlB2 = "b2-staging.autodesk.com";
                this.baseUrlB4 = "b4-staging.autodesk.com";
                this.baseUrlAccounts = "accounts-staging.autodesk.com";
            }
            else {
                this.baseUrlB2 = "b2.autodesk.com";
                this.baseUrlB4 = "b4.autodesk.com";
                this.baseUrlAccounts = "accounts.autodesk.com";
            }
        }

        public bool IsStaging { get; }
        public bool IsLoggedIn { get; set; }

        public void Login() {
            var username = this.IsStaging ? "user@user.com" : "user2@user2.com";
            var password = "password";

            IsLoggedIn = false;
            this.connectedHost = null;

            // initialize client
            client = new RestClient();
            client.CookieContainer = new CookieContainer();

            // login page
            var req1 = new RestRequest($"https://{baseUrlB2}/login");
            var res1 = client.Execute(req1);
            if (!res1.IsSuccessful) throw new Exception(res1.StatusCode + " " + res1.StatusDescription);

            // click login
            var req2 = new RestRequest($"https://{baseUrlB2}//consumer?openid_identifier=https://{baseUrlAccounts}&inIFrame=1");
            var res2 = client.Execute(req2);
            if (!res2.IsSuccessful) throw new Exception(res2.StatusCode + " " + res2.StatusDescription);
            var body2 = res2.Content;

            var authKey = Regex.Match(body2, @"AuthKey%3D([a-z0-9-]+)""").Groups[1];
            var token = Regex.Match(body2, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1];

            // submit username
            var req3 = new RestRequest(
                $"https://{baseUrlAccounts}/Authentication/IsExistingUser?viewmode=iframe&ReturnUrl=%2Fauthorize%3Fviewmode%3Diframe%26lang%3Den-US%26realm%3D{baseUrlB2}%26ctx%3Dbim360w%26AuthKey%3D{authKey}",
                Method.POST);

            req3.AddParameter("__RequestVerificationToken", token);
            req3.AddParameter("Username", username);

            var res3 = client.Execute(req3);
            if (!res3.IsSuccessful) throw new Exception(res3.StatusCode + " " + res3.StatusDescription);

            // submit password
            var req4 = new RestRequest(
                $"https://{baseUrlAccounts}/Authentication/LogOn?viewmode=iframe&ReturnUrl=%2Fauthorize%3Fviewmode%3Diframe%26lang%3Den-US%26realm%3D{baseUrlB2}%26ctx%3Dbim360w%26AuthKey%3D{authKey}",
                Method.POST);

            req4.AddParameter("__RequestVerificationToken", token);
            req4.AddParameter("queryStrings", $"?viewmode=iframe&ReturnUrl=%2Fauthorize%3Fviewmode%3Diframe%26lang%3Den-US%26realm%3D{baseUrlB2}%26ctx%3Dbim360w%26AuthKey%3D{authKey}");
            req4.AddParameter("signinThrottledMessage", "You+have+made+too+many+sign+in+attempts+recently.+Please+try+again+later.");
            req4.AddParameter("UserName", username);
            req4.AddParameter("Password", password);
            req4.AddParameter("RememberMe", false);

            var res4 = client.Execute(req4);
            if (!res4.IsSuccessful) throw new Exception(res4.StatusCode + " " + res4.StatusDescription);
            var body4 = res4.Content;

            // finalize auth
            var req5 = new RestRequest(
                $"https://{baseUrlB2}/consumer?is_return=true&isIFrame=true",
                Method.POST);

            req5.AddParameter("openid.claimed_id", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.claimed_id"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.identity", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.identity"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.sig", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.sig"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.signed", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.signed"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.assoc_handle", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.assoc_handle"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.op_endpoint", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.op_endpoint"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.return_to", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.return_to"" value=""([^""]*)"" />").Groups[1].Value.Replace("&amp;", "&"));
            req5.AddParameter("openid.response_nonce", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.response_nonce"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.mode", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.mode"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.ns", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.ns"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.ns.alias3", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.ns\.alias3"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias3.request_token", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias3\.request_token"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias3.scope", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias3\.scope"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.ns.alias4", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.ns\.alias4"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.mode", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.mode"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.type.alias1", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.type\.alias1"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.value.alias1", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.value\.alias1"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.type.alias2", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.type\.alias2"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.value.alias2", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.value\.alias2"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.type.alias3", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.type\.alias3"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.value.alias3", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.value\.alias3"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.type.alias4", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.type\.alias4"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.value.alias4", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.value\.alias4"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.type.alias5", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.type\.alias5"" value=""([^""]*)"" />").Groups[1]);
            req5.AddParameter("openid.alias4.value.alias5", Regex.Match(body4, @"<input type=""hidden"" name=""openid\.alias4\.value\.alias5"" value=""([^""]*)"" />").Groups[1]);

            var res5 = client.Execute(req5);
            if (!res5.IsSuccessful) throw new Exception(res5.StatusCode + " " + res5.StatusDescription);

            foreach (var cookie in res5.Cookies) {
                // cookies not setting from .autodesk.com
                // remove . from beginning of domain and set path to /
                var fixedDomain = cookie.Domain.StartsWith(".") ? cookie.Domain.Remove(0, 1) : cookie.Domain;
                var c = new Cookie(cookie.Name, cookie.Value, "/", fixedDomain);
                client.CookieContainer.Add(c);
            }

            var b2Cookies = client.CookieContainer.GetCookies(new Uri($"https://{baseUrlB2}")).Cast<Cookie>();

            var g_id = b2Cookies.Where(x => x.Name == "g_id").SingleOrDefault();
            var g_s = b2Cookies.Where(x => x.Name == "g_s").SingleOrDefault();
            var oi_oid = b2Cookies.Where(x => x.Name == "oi_oid").SingleOrDefault();

            if (g_id == null || g_s == null || oi_oid == null || g_id.Value == "\"\"" || g_s.Value == "\"\"" || oi_oid.Value == "\"\"") {
                Program.Logger.Error($"Login failed");
                throw new Exception("Login failed");
            }
            else {
                IsLoggedIn = true;
                Program.Logger.Info($"Login successful");
            }
        }
        
        public List<ICompany> GetCompanyListing() {
            if (!this.IsLoggedIn) throw new Exception("Not logged in");

            var req = new RestRequest($"https://{baseUrlB2}/GlueApiServlet?api=getHostListing");
            req.AddHeader("X-Requested-With", "XMLHttpRequest");
            req.AddHeader("Referer", $"https://{baseUrlB2}/access");

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var hostListing = JsonConvert.DeserializeObject<HostListing>(res.Content);

            var companyListing = hostListing.list.Select(host => (ICompany)host).ToList();
            companyListing.ForEach(x => x.Ftp = this);

            return companyListing;
        }

        private void ConnectToCompany(ICompany company) {
            this.connectedHost = null;

            var b2Cookies = client.CookieContainer.GetCookies(new Uri($"https://{baseUrlB2}")).Cast<Cookie>();

            // set g_as, g_h cookies to ""
            foreach (var cookie in b2Cookies) {
                if (cookie.Name == "g_as" || cookie.Name == "g_h") {
                    cookie.Value = "\"\"";
                }
            }

            var req = new RestRequest($"https://{baseUrlB2}/GlueHostServlet?host={company.Id}");
            req.AddHeader("X-Requested-With", "XMLHttpRequest");
            req.AddHeader("Referer", $"https://{baseUrlB2}/host");

            // cookies not set from 302 Redirect page
            // disable redirects for this request to get cookies
            client.FollowRedirects = false;
            var res = client.Execute(req);
            if (res.StatusCode != HttpStatusCode.Redirect) throw new Exception(res.StatusCode.ToString());
            client.FollowRedirects = true;

            foreach (var cookie in res.Cookies) {
                // cookies not setting from .autodesk.com
                // remove . from beginning of domain and set path to /
                var fixedDomain = cookie.Domain.StartsWith(".") ? cookie.Domain.Remove(0, 1) : cookie.Domain;
                var c = new Cookie(cookie.Name, cookie.Value, "/", fixedDomain);
                client.CookieContainer.Add(c);
            }

            var req2 = new RestRequest(res.ResponseUri);
            var res2 = client.Execute(req2);

            b2Cookies = client.CookieContainer.GetCookies(new Uri($"https://{baseUrlB2}")).Cast<Cookie>();

            var g_as = b2Cookies.Where(x => x.Name == "g_as").SingleOrDefault();
            var g_h = b2Cookies.Where(x => x.Name == "g_h").SingleOrDefault();

            // if g_as, g_h cookies not set
            if (g_as == null || g_h == null || g_as.Value == "\"\"" || g_h.Value == "\"\"") {
                throw new Exception("Could not connect to host");
            }
            else {
                this.connectedHost = (GlueCompany)company;
            }
        }

        private Signature GetSignature(GlueCompany company) {
            if (this.connectedHost != company) {
                ConnectToCompany(company);
            }

            var req = new RestRequest($"https://{baseUrlB2}/GlueApiServlet?api=getSignature");
            req.AddHeader("X-Requested-With", "XMLHttpRequest");
            req.AddHeader("Referer", $"https://{baseUrlB2}/i/{company.company_id}");

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            return JsonConvert.DeserializeObject<Signature>(res.Content);
        }

        public List<IProject> GetProjectListing(ICompany company) {
            if (this.connectedHost != company) {
                ConnectToCompany(company);
            }

            var sig = GetSignature((GlueCompany)company);

            var req = new RestRequest($"https://{baseUrlB4}:443/api/project/v1/list.json");
            req.AddParameter("company_id", this.connectedHost.Id);
            req.AddParameter("api_key", sig.publicKey);
            req.AddParameter("auth_token", sig.authToken);
            req.AddParameter("timestamp", sig.timestamp);
            req.AddParameter("sig", sig.signature);
            req.AddParameter("page", 1);
            req.AddParameter("page_size", 100);
            //req.AddParameter("sort", Enum.GetName(typeof(SortOptions), sort)); // name_asc, name desc, date_asc, date_desc
            //req.AddParameter("sterm", searchTerm);

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var projects = JsonConvert.DeserializeObject<ProjectListing>(res.Content);

            var projectListing = new List<IProject>();

            // TODO set company in constructor to avoid public set in interface
            foreach (var project in projects.project_list) {
                project.Company = company;
                projectListing.Add(project);
            }

            return projectListing;
        }

        public List<IFile> GetFileListing(IProject project) {
            if (this.connectedHost != project.Company) {
                ConnectToCompany(project.Company);
            }

            var sig = GetSignature((GlueCompany)project.Company);

            //string modelTypeString;

            //switch (modelType) {
            //    case ModelType.Both:
            //        modelTypeString = "";
            //        break;
            //    case ModelType.Single:
            //        modelTypeString = "SINGLE";
            //        break;
            //    case ModelType.Merged:
            //        modelTypeString = "MERGED";
            //        break;
            //    default:
            //        modelTypeString = string.Empty;
            //        break;
            //}

            var req = new RestRequest($"https://{baseUrlB4}:443/api/model/v1/list.json");
            req.AddParameter("company_id", this.connectedHost.Id);
            req.AddParameter("api_key", sig.publicKey);
            req.AddParameter("auth_token", sig.authToken);
            req.AddParameter("timestamp", sig.timestamp);
            req.AddParameter("sig", sig.signature);
            req.AddParameter("project_id", project.Id);
            //req.AddParameter("page", page);
            //req.AddParameter("page_size", pageSize);
            //req.AddParameter("sort", Enum.GetName(typeof(SortOptions), sort));
            //req.AddParameter("model_type", modelTypeString);
            //req.AddParameter("is_latest", latestVersionOnly ? 1 : 0);
            //req.AddParameter("model_name", modelName);
            req.AddParameter("lightweight", 1); // need this to get all files on single page

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var files = JsonConvert.DeserializeObject<ModelListing>(res.Content);

            var fileListing = new List<IFile>();

            // TODO set project in constructor to avoid public set in interface
            foreach (var file in files.model_list) {
                file.Project = project;
                fileListing.Add(file);
            }

            return fileListing;
        }

        private GlueFile GetModelInfo(GlueCompany company, GlueProject project, string model_id, string version_id = null) {
            if (this.connectedHost != company) {
                ConnectToCompany(company);
            }

            var sig = GetSignature(company);

            var req = new RestRequest($"https://{baseUrlB4}:443/api/model/v2/info.json");
            req.AddParameter("company_id", this.connectedHost.company_id);
            req.AddParameter("api_key", sig.publicKey);
            req.AddParameter("auth_token", sig.authToken);
            req.AddParameter("timestamp", sig.timestamp);
            req.AddParameter("sig", sig.signature);
            req.AddParameter("model_id", model_id);
            req.AddParameter("version_id", version_id);

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var modelInfo = JsonConvert.DeserializeObject<GlueFile>(res.Content);
            modelInfo.Project = project;

            return modelInfo;
        }

        private void AggregateModel(GlueCompany company, string model_id) {
            if (this.connectedHost != company) {
                ConnectToCompany(company);
            }

            var sig = GetSignature(company);

            var req = new RestRequest($"https://{baseUrlB4}:443/api/model/v1/aggregate.json", Method.POST);
            req.AddParameter("company_id", this.connectedHost.company_id);
            req.AddParameter("api_key", sig.publicKey);
            req.AddParameter("auth_token", sig.authToken);
            req.AddParameter("timestamp", sig.timestamp);
            req.AddParameter("sig", sig.signature);
            req.AddParameter("model_id", model_id);

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());
        }

        private void WaitUntilParsed(GlueFile file) {
            int interval = 10000;
            int timeout = 600000;
            var timeWaited = 0;

            do {
                if (timeWaited >= timeout) {
                    throw new Exception("Model aggregation timeout");
                }

                Thread.Sleep(interval);
                timeWaited += interval;
            } while (this.GetModelInfo((GlueCompany)file.Project.Company, (GlueProject)file.Project, file.Id).file_parsed_status != 1);
        }

        public FileInfo DownloadFile(IFile file, string downloadLocation) {
            var glueCompany = (GlueCompany)file.Project.Company;
            var glueFile = (GlueFile)file;

            if (this.connectedHost != file.Project.Company) {
                ConnectToCompany(glueCompany);
            }

            bool nwd = false;

            var startTime = DateTime.Now;

            var isMergedModel = glueFile.is_merged_model == 1;

            // 2 is misc file that can't be parsed
            var isParsed = glueFile.file_parsed_status == 1 || glueFile.file_parsed_status == 2;

            if (isMergedModel) {
                nwd = true;
                this.AggregateModel(glueCompany, file.Id);
            }

            if (!isParsed) {
                WaitUntilParsed(glueFile);
            }

            Directory.CreateDirectory(downloadLocation);

            var filepath = Path.Combine(downloadLocation, glueFile.Name);

            // moved to GlueFile.Name
            //if (nwd) {
            //    filepath += ".nwd";
            //}

            var sig = GetSignature(glueCompany);

            var req = new RestRequest($"https://{baseUrlB4}:443/api/model/v1/download");
            req.AddParameter("company_id", this.connectedHost.company_id);
            req.AddParameter("api_key", sig.publicKey);
            req.AddParameter("auth_token", sig.authToken);
            req.AddParameter("timestamp", sig.timestamp);
            req.AddParameter("sig", sig.signature);
            req.AddParameter("model_id", file.Id);
            req.AddParameter("alt_format", nwd ? "nwd" : "");

            using (var downloadStream = new FileStream(filepath, FileMode.Create, FileAccess.Write)) {
                req.ResponseWriter = responseStream => {
                    using (responseStream) {
                        responseStream.CopyTo(downloadStream);
                    }
                };

                Program.Logger.Info($"Download starting for {filepath}");

                client.DownloadData(req, true);

                Program.Logger.Info(downloadStream.Length/1024/1024 + "MB downloaded");
            }

            var fileInfo = new FileInfo(filepath);

            if (fileInfo.Exists) {
                var fileModifiedTime = File.GetLastWriteTime(filepath);
                var fileIsNewer = DateTime.Compare(startTime, fileModifiedTime) < 0;

                if (!fileIsNewer) {
                    Program.Logger.Error("Download failed");
                    throw new Exception("Download failed");
                }
            }
            else {
                Program.Logger.Error("Download failed");
                throw new Exception("Download failed");
            }

            return fileInfo;
        }

        public IFolder GetFolderInfo(IProject project, string folderId) {
            return _GetFolderInfo(project, folderId, false);
        }

        private GlueFolder _GetFolderInfo(IProject project, string folderId, bool treeOnly) {
            var glueCompany = (GlueCompany)project.Company;

            if (this.connectedHost != glueCompany) {
                ConnectToCompany(glueCompany);
            }

            var sig = GetSignature(glueCompany);

            var req = new RestRequest($"https://{baseUrlB4}:443/api/project/v1/tree.json");
            req.AddParameter("company_id", this.connectedHost.company_id);
            req.AddParameter("api_key", sig.publicKey);
            req.AddParameter("auth_token", sig.authToken);
            req.AddParameter("timestamp", sig.timestamp);
            req.AddParameter("sig", sig.signature);
            req.AddParameter("project_id", project.Id);
            req.AddParameter("folder_id", folderId);
            req.AddParameter("lightweight", 1);
            req.AddParameter("get_folder_structure", treeOnly ? 1 : 0);

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var folder = JsonConvert.DeserializeObject<GlueFolder>(res.Content);
            folder.Project = project;

            return folder;
        }

        public IFile GetFileInfo(IProject project, string fileId) {
            return GetModelInfo((GlueCompany)project.Company, (GlueProject)project, fileId);
        }

        //public List<IFolderContent> Search(IProject project, string searchTerm) {
        //    GlueFolder tree = this._GetFolderInfo(project, null, true);

        //    GlueFolderContent treeAsContent = new GlueFolderContent {
        //        folder_contents = tree.folder_only_structure,
        //        object_id = null
        //    };

        //    // local function
        //    List<GlueFolderContent> getFoldersRecursive(GlueFolderContent content) {
        //        var folders = new List<GlueFolderContent>();

        //        if (content.folder_contents != null) {
        //            folders.AddRange(content.folder_contents);

        //            for (int i = 0; i < content.folder_contents.Count; i++) {
        //                var subfolder = content.folder_contents[i];
        //                folders.AddRange(getFoldersRecursive(subfolder));
        //            }
        //        }

        //        return folders;
        //    }

        //    List<IFolderContent> allFolderContents = getFoldersRecursive(treeAsContent).Cast<IFolderContent>().ToList();

        //    List<IFile> allFiles = this.GetFileListing(project);


        //    List<IFolderContent> matchingFolderContents = allFolderContents.Where(x => 
        //        x.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) != -1   // folder name contains search term
        //        || x.Id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) != -1) // folder id contains search term
        //        .ToList();


        //    List<IFile> matchingFiles = allFiles.Where(x =>
        //        x.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) != -1   // file name contains search term
        //        || x.Id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) != -1) // file id contains search term
        //        .ToList();

        //    List<IFolderContent> filesAsContents = matchingFiles.Select(x => (IFolderContent)new GenericFolderContent {
        //        ContentType = ContentType.File,
        //        Name = x.Name,
        //        Id = x.Id,
        //        Project = project, 
        //        DateModified = x.DateModified
        //    }).ToList();


        //    List<IFolderContent> allMatchingContents = matchingFolderContents.Concat(filesAsContents).ToList();

        //    return allMatchingContents;
        //}

        public void PrintProjectTree(IProject project) {
            var tree = (GlueFolder)this.GetFolderInfo(project, null);
            var treeid = tree.Id;
            var treename = tree.Name;

            var test = (GlueFolder)this._GetFolderInfo(project, null, true);
            var idx = test.Id;
            var namex = test.Name;

            var treeAsContent = new GlueFolderContent {
                folder_contents = tree.folder_tree
            };

            void printFolderContent(GlueFolderContent content, int level) {
                foreach (GlueFolderContent subContent in content.folder_contents) {
                    for (int i = 0; i < level; i++) {
                        Console.Write("    ");
                    }

                    Console.WriteLine(subContent.name.Replace('+', ' ').Replace("%26", "&").Replace("%2b", "+") + " - " + subContent.object_id);

                    if (subContent.folder_contents != null) {
                        printFolderContent(subContent, level + 1);
                    }
                }
            }

            printFolderContent(treeAsContent, 0);
        }
    }
}
