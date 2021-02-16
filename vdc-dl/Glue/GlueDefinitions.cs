using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VdcDl.Glue {
    public class Signature {
        public string publicKey;
        public string timestamp;
        public string signature;
        public string authToken;
        public string company;
    }

    public class HostListing {
        public GlueCompany[] list;
        public int? company_count;
        public int? locked_company_count;
        public int? expired_company_count;
    }

    public class GlueCompany : ICompany {
        public string company_id;
        public string company_name;
        public string user_type;
        public string contract_type;
        public string company_guid;
        public string company_hq_guid;
        public string[] permissions;

        public string Name { get => company_name; }
        public string Id { get => company_id; }
        public IFtp Ftp { get; set; }
    }

    public class ProjectListing {
        public GlueProject[] project_list;
        public int? page;
        public int? page_size;
        public int? total_result_size;
        public int? more_pages;
    }

    public class GlueProject : IProject {
        public GlueFolderContent[] folder_tree;
        public object[] recent_model_info;
        public string project_id;
        public string project_name;
        public string company_id;
        public string created_date;
        public string modify_date;
        public string cmic_company_code;
        public string cmic_project_code;
        public string cw_project_code;
        public string start_date;
        public string end_date;
        public string status;
        public int? value_number;
        public string value_currency;
        public bool? has_views;
        public bool? has_markups;
        public bool? has_clashes;
        public bool? has_points;
        public int? total_member_count;
        public int? total_project_admin_count;
        public int? total_views_count;
        public int? total_markups_count;
        public string last_activity_date;
        public string[] permissions;
        public string navisworks_version;

        public ICompany Company { get; set; }
        public string Name { get => project_name; }
        public string Id { get => project_id; }
    }

    public enum GlueFolderContentType {
        MODEL,
        FOLDER
    }

    public enum GlueModelType {
        Both,
        Single,
        Merged
    }

    public class GlueFolder : IFolder {
        public List<GlueFolderContent> folder_tree;
        public List<GlueFolderContent> folder_only_structure;
        public string project_id;
        public string project_name;
        public string company_id;
        public string created_date;
        public string modify_date;
        public string last_activity_date;

        public List<IFolderContent> Contents {
            get {
                // if regular folder
                if (folder_tree != null) {
                    List<GlueFolderContent> glueFolderContents = folder_tree.Single().folder_contents ?? new List<GlueFolderContent>();
                    glueFolderContents.ForEach(x => x.Project = this.Project);

                    var files = new List<IFile>();

                    foreach (var c in glueFolderContents.Where(x => x.ContentType == ContentType.File)) {
                        var file = new GlueFile {
                            model_id = c.object_id,
                            model_name = c.name, // keep glue formatting
                            model_version = c.version,
                            Project = c.Project,
                            modified_date = c.modified_date
                        };

                        files.Add(file);
                    }


                    var folders = new List<IFolder>();

                    foreach (var c in glueFolderContents.Where(x => x.ContentType == ContentType.Folder)) {
                        throw new NotImplementedException();

                        var folder = new GlueFolder {

                        };

                        folders.Add(folder);
                    }


                    var content = new List<IFolderContent>();
                    content.AddRange(files);
                    content.AddRange(folders);

                    return content;
                }
                // if tree folder (no files)
                else if (folder_only_structure != null) {
                    return folder_only_structure.Select(x => (IFolderContent)x).ToList();
                }
                else {
                    return null;
                }
            }

            set => Contents = value;
        }

        public IProject Project { get; set; }

        //public string Name => Contents.First().Name;
        public string Name {
            get {
                if (folder_tree != null) {
                    return folder_tree.Single().Name;
                }
                else {
                    return null;
                }
            }
        }


        //public string Id => Contents.First().Id;
        //public string Id => folder_tree.Single().Id;
        public string Id {
            get {
                if (folder_tree != null) {
                    return folder_tree.Single().Id;
                }
                else {
                    return null;
                }
            }
        }

        public DateTime DateModified =>
            DateTime.ParseExact(modify_date,
            "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.CurrentCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal) // parse as UTC time
            .ToLocalTime(); // convert to local time;

        public ContentType ContentType => ContentType.Folder;
    }

    public class GlueFolderContent : IFolderContent {
        public GlueFolderContentType type;
        public string name;
        public string object_id;
        public string action_id;
        public string version_id;
        public int? version;
        public string created_by;
        public string created_by_id;
        public string created_by_first_name;
        public string created_by_last_name;
        public string created_date;
        public string modified_date;
        public string parent_folder_id;
        public int? is_merged_model;
        public int? file_parsed_status;
        public int? ever_published;
        public int? latest_published_version_number;
        public int? ever_synchronized;
        public int? merged_model_available;
        public string merged_model_aggregation_status;
        public bool? has_views;
        public bool? has_markups;
        public bool? has_clashes;
        public List<GlueFolderContent> folder_contents;

        public IProject Project { get; set; }

        public string Name => System.Web.HttpUtility.UrlDecode(this.name);

        public string Id => object_id;

        public DateTime DateModified =>
            DateTime.ParseExact(modified_date,
            "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.CurrentCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal) // parse as UTC time
            .ToLocalTime(); // convert to local time;

        public ContentType ContentType {
            get {
                if (this.type == GlueFolderContentType.FOLDER) {
                    return ContentType.Folder;
                }
                else {
                    return ContentType.File;
                }
            }
        }
    }

    public class ModelListing {
        public GlueFile[] model_list;
        public int? page;
        public int? page_size;
        public int? total_result_size;
        public int? more_pages;
    }

    public class GlueFile : IFile {
        public GlueFile[] version_history;
        public string action_id;
        public string company_id;
        public string project_id;
        public string model_id;
        public int? model_version;
        public string model_version_id;
        public string model_name;
        public string created_by;
        public string created_by_first_name;
        public string created_by_last_name;
        public string created_date;
        public string modified_by;
        public string modified_date;
        public string parent_folder_id;
        public int? is_merged_model;
        public int? is_folder;
        public int? is_deleted;
        public int? metadata_version;
        public int? file_parsed_status;
        public string model_location;
        public string dod_status;
        public int? published;
        public int? synchronized;
        public int? ever_published;
        public int? latest_published_version_number;
        public int? ever_synchronized;
        public bool? has_views;
        public bool? has_markups;
        public bool? has_clashes;
        public string storage_driver;
        public int? merged_model_available;
        public string das_job_id;
        public string das_status;
        public string dod_job_id;
        public string dod_model_id;
        public string dod_model_version_id;

        public int Version { get => model_version ?? -1; }

        public IProject Project { get; set; }

        public string Name {
            get {
                var name = System.Web.HttpUtility.UrlDecode(this.model_name);

                // add .nwd to merged model name (has no file extension on glue)
                if (this.is_merged_model == 1) {
                    name += ".nwd";
                }

                return name;
            }
        }

        public string BaseName => Path.GetFileNameWithoutExtension(this.Name);

        public string FileExt {
            get {
                var ext = Path.GetExtension(this.Name);

                // remove dot from file extension
                if (ext.StartsWith(".")) {
                    ext = ext.Substring(1);
                }

                return ext;
            }
        }

        public string Id { get => model_id; }

        public DateTime DateModified {
            get =>
                DateTime.ParseExact(modified_date,
                "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal) // parse as UTC time
                .ToLocalTime(); // convert to local time
        }

        public ContentType ContentType => ContentType.File;
    }

    public class AggregationResponse {
        public string update_time;
        public string model_id;
        public AggregationStatus status;
    }

    public enum AggregationStatus {
        Waiting,
        Ready
    }

    public enum SortOptions {
        name_asc,
        name_desc,
        date_asc,
        date_desc
    }
}
