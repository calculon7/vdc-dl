using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Text.RegularExpressions;

namespace VdcDl.Procore {
    class ProcoreSession : IFtp {
        RestClient client;

        public bool IsStaging => false;

        public bool IsLoggedIn { get; set; }

        public ProcoreSession() {
            IsLoggedIn = false;

            // initialize client
            client = new RestClient();
            client.CookieContainer = new CookieContainer();
        }

        public void Login() {
            var username = "user@user.com";
            var password = "password";

            IsLoggedIn = false;

            // login page
            var req1 = new RestRequest("https://login.procore.com");
            var res1 = client.Execute(req1);
            if (!res1.IsSuccessful) throw new Exception(res1.StatusCode + " " + res1.StatusDescription);
            var body1 = res1.Content;
            var csrf_token = Regex.Match(body1, @"<meta name=""csrf-token"" content=""([^""]+)"" />").Groups[1].Value;


            // login
            var req2 = new RestRequest("https://login.procore.com/sessions", Method.POST);
            req2.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

            req2.AddParameter("utf8", "✓");
            req2.AddParameter("authenticity_token", csrf_token);
            req2.AddParameter("session[sso_target_url]", "");
            req2.AddParameter("session[email]", username);
            req2.AddParameter("session[password]", password);

            var res2 = client.Execute(req2);
            if (!res2.IsSuccessful) throw new Exception(res2.StatusCode + " " + res2.StatusDescription);

            if (res2.ResponseUri.ToString() == "https://app.procore.com/account/select_company") {
                IsLoggedIn = true;
                Program.Logger.Info($"Login successful");
            }
            else {
                Program.Logger.Error($"Login failed");
                throw new Exception("Login failed");
            }
        }

        public List<ICompany> GetCompanyListing() {
            var req = new RestRequest("https://app.procore.com/vapid/companies");

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var hosts = JsonConvert.DeserializeObject<List<ProcoreCompany>>(res.Content);

            var companies = hosts.Select(x => (ICompany)x).ToList();
            companies.ForEach(x => x.Ftp = this);

            return companies;
        }

        public List<IProject> GetProjectListing(ICompany company) {
            var req = new RestRequest("https://app.procore.com/vapid/projects");
            req.AddParameter("company_id", company.Id);

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var projects = JsonConvert.DeserializeObject<List<ProcoreProject>>(res.Content);

            var projectListing = new List<IProject>();

            foreach (var project in projects) {
                project.Company = company;
                projectListing.Add(project);
            }

            return projectListing;
        }

        public List<IFile> GetFileListing(IProject project) {
            int projectId = int.Parse(project.Id);
            var tree = GetFolderTree((ProcoreProject)project);
            var files = tree.GetFilesRecursive();

            var fileListing = new List<IFile>();

            foreach (var file in files) {
                file.Project = project;
                fileListing.Add(file);
            }

            return fileListing;
        }

        public FileInfo DownloadFile(IFile file, string downloadLocation) {
            var req1 = new RestRequest($"https://app.procore.com/rest/v0.0/projects/{file.Project.Id}/documents/{file.Id}/download_file");
            var res1 = client.Execute(req1);
            if (!res1.IsSuccessful) throw new Exception(res1.StatusCode + " " + res1.StatusDescription);

            var body1 = JsonConvert.DeserializeAnonymousType(res1.Content, new { url = "" });
            var downloadUrl = body1.url;


            client.FollowRedirects = false;
            var req2 = new RestRequest(downloadUrl);
            var res2 = client.Execute(req2);
            client.FollowRedirects = true;
            if (res2.StatusCode != HttpStatusCode.Found) throw new Exception(res2.StatusCode + " Redirect failed");

            var cdnDownloadUrl = res2.Headers.Where(x => x.Name.Equals("Location", StringComparison.OrdinalIgnoreCase)).Single().Value.ToString();


            Directory.CreateDirectory(downloadLocation);
            var filepath = Path.Combine(downloadLocation, file.Name);

            var downloadStartTime = DateTime.Now;

            var req = new RestRequest(cdnDownloadUrl);

            using (var downloadStream = new FileStream(filepath, FileMode.Create, FileAccess.Write)) {
                req.ResponseWriter = responseStream => {
                    using (responseStream) {
                        responseStream.CopyTo(downloadStream);
                    }
                };

                Program.Logger.Info($"Download starting for {filepath}");

                client.DownloadData(req, true);

                Program.Logger.Info(downloadStream.Length / 1024 / 1024 + " MB downloaded");
            }


            var fileInfo = new FileInfo(filepath);

            if (fileInfo.Exists) {
                var fileModifiedTime = fileInfo.LastWriteTime;

                // if file is newer
                if (DateTime.Compare(downloadStartTime, fileModifiedTime) < 0) {
                    return fileInfo;
                }
                else {
                    Program.Logger.Error("Download failed");
                    throw new Exception("Download failed");
                }
            }
            else {
                Program.Logger.Error("Download failed");
                throw new Exception("Download failed");
            }
        }

        public IFile GetFileInfo(IProject project, string fileId) {
            var req = new RestRequest($"https://app.procore.com/vapid/files/{fileId}");
            //req.AddParameter("view", "viewable_url"); // optional
            req.AddParameter("project_id", project.Id);
            req.AddParameter("show_latest_version_only", false);

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());

            var file = JsonConvert.DeserializeObject<ProcoreFile>(res.Content);
            file.Project = project;

            return file;
        }

        public IFolder GetFolderInfo(IProject project, string folderId) {
            int? id;

            try {
                id = int.Parse(folderId);
            }
            catch {
                id = null;
            }

            var folder = (IFolder)_GetFolderInfo((ProcoreProject)project, id);

            return folder;
        }

        private ProcoreFolder _GetFolderInfo(ProcoreProject project, int? folder_id = null) {
            RestRequest req;

            if (folder_id == null) {
                req = new RestRequest("https://app.procore.com/vapid/folders");
                req.AddParameter("show_latest_file_version_only", true);
                req.AddParameter("view", "web_normal");
                req.AddParameter("project_id", project.id);
            }
            else {
                req = new RestRequest($"https://app.procore.com/vapid/folders/{folder_id}");
                req.AddParameter("show_latest_file_version_only", true);
                req.AddParameter("view", "web_normal");
                req.AddParameter("project_id", project.id);
            }

            var res = client.Execute(req);
            if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());
            var body = res.Content;

            var folder = JsonConvert.DeserializeObject<ProcoreFolder>(body);
            folder.Project = project;

            folder.Contents.ForEach(x => x.Project = project);

            return folder;
        }

        private ProcoreFolder GetFolderTree(ProcoreProject project, int? root_folder_id = null) {
            Console.WriteLine(root_folder_id);
            var tree = this._GetFolderInfo(project, root_folder_id);

            for (int i = 0; i < tree.folders.Length; i++) {
                if (tree.folders[i].has_children) {
                    tree.folders[i] = GetFolderTree(project, tree.folders[i].id);
                }
            }

            tree.populated = true;

            return tree;
        }

        //public List<IFolderContent> Search(IProject project, string searchTerm) {
        //    // TODO

        //    List<GenericFolderContent> procoreWebSearch(int page) {
        //        var req = new RestRequest($"https://app.procore.com/685806/project/documents/filter_index");
        //        req.AddParameter("filters[search]", searchTerm);
        //        req.AddParameter("page", page);
        //        req.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

        //        var res = client.Execute(req);
        //        if (!res.IsSuccessful) throw new Exception(res.StatusCode.ToString());
        //        var body = res.Content;

        //        var doc = new HtmlDocument();
        //        doc.LoadHtml(body);

        //        var root = doc.DocumentNode;
        //        var tbody = root.SelectSingleNode("//tbody");
        //        var trows = tbody.SelectNodes("tr");

        //        var contents = new List<GenericFolderContent>();

        //        if (trows.Count != 1 && trows.First().InnerText.Trim() != "No results found") {
        //            foreach (var tr in trows) {
        //                var content = new GenericFolderContent { Project = project };

        //                content.Name = tr.SelectNodes("td")[1].InnerText.Trim().Replace("&amp;", "&");

        //                // ignore if search term not in name
        //                if (content.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) == -1) {
        //                    continue;
        //                }

        //                var altText = tr.SelectNodes("td")[1].SelectSingleNode("img").Attributes["alt"].Value; // "Leaf" or "Folder"

        //                if (altText == "Folder") {
        //                    content.ContentType = ContentType.Folder;
        //                }
        //                else if (altText == "Leaf") {
        //                    content.ContentType = ContentType.File;
        //                }
        //                else {
        //                    throw new Exception("Unexpected altText");
        //                }

        //                var href = tr.SelectNodes("td/a")[0].Attributes["href"].Value;
        //                content.Id = href.Substring(href.LastIndexOf('/') + 1);

        //                // "03/29/20 at 02:49 pm EDT"
        //                var timestamp = tr.SelectNodes("td")[4].InnerText;
        //                var match = Regex.Match(timestamp, @"(?<month>\d{2})/(?<day>\d{2})/(?<year>\d{2}) at (?<hour>\d{2}):(?<minute>\d{2}) (?<meridiem>am|pm) (?<timezone>[A-Z]+)");

        //                var year = int.Parse(match.Groups["year"].Value) + 2000; // assume modified after 2000
        //                var month = int.Parse(match.Groups["month"].Value);
        //                var day = int.Parse(match.Groups["day"].Value);
        //                var hour = int.Parse(match.Groups["hour"].Value);
        //                var minute = int.Parse(match.Groups["minute"].Value);
        //                if (match.Groups["meridiem"].Value == "pm" && hour < 12) {
        //                    hour += 12;
        //                }
        //                //var timezone = match.Groups["timezone"].Value; // ignore timezone

        //                content.DateModified = new DateTime(year, month, day, hour, minute, 0);

        //                contents.Add(content);
        //            }
        //        }

        //        return contents;
        //    }

        //    var searchResults = new List<IFolderContent>();

        //    int i = 0;

        //    while (true) {
        //        i++;
        //        var results = procoreWebSearch(i);
        //        searchResults.AddRange(results);

        //        if (results.Count < 100) {
        //            break; // reached end (100 per page)
        //        }
        //    }

        //    return searchResults;
        //}

        //public void PrintProjectTree(IProject project) {
        //    var tree = this.GetFolderTree((ProcoreProject)project);

        //    void printFolderContent(ProcoreFolder folder, int level) {
        //        foreach (ProcoreFolder subfolder in folder.folders) {
        //            for (int i = 0; i < level; i++) {
        //                Console.Write("    ");
        //            }

        //            Console.WriteLine(subfolder.name + " - " + subfolder.id);

        //            printFolderContent(subfolder, level + 1);
        //        }

        //        foreach (ProcoreFile file in folder.files) {
        //            for (int i = 0; i < level; i++) {
        //                Console.Write("    ");
        //            }

        //            Console.WriteLine(file.name + " - " + file.id);
        //        }
        //    }

        //    printFolderContent(tree, 0);
        //}
    }
}
