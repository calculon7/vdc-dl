using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VdcDl.Procore {
    public class ProcoreCompany : ICompany{
        public int? id;
        public bool is_active;
        public string name;

        public string Name => name;

        public string Id => id.ToString();

        public IFtp Ftp { get; set; }
    }

    public class ProcoreProject : IProject {
        public int? id;
        public string name;
        public string display_name;
        public string project_number;
        public string address;
        public string city;
        public string state_code;
        public string country_code;
        public string zip;
        public string county;
        public string time_zone;
        public double? latitude;
        public double? longitude;
        public string stage;
        public string phone;
        public string created_at;
        public string updated_at;
        public bool active;
        public string origin_id;
        public string origin_data;
        public string origin_code;
        public string estimated_value;
        public int? project_region_id;
        public object project_bid_type_id;
        public object project_owner_type_id;
        public object photo_id;
        public ProcoreCompany company;

        public ICompany Company { get; set; }

        public string Name => name;

        public string Id => id.ToString();
    }

    public class ProcoreFolder : IFolder {
        public int? id;
        public string name;
        public string name_with_path;
        public int? parent_id;
        public string updated_at;
        public bool is_deleted;
        public bool is_recycle_bin;
        public bool is_tracked;
        public ProcoreFolder tracked_folder;
        public bool has_children;
        public bool has_children_files;
        public bool has_children_folders;
        public ProcoreFolder[] folders;
        public ProcoreFile[] files;
        public bool @private;
        public bool read_only;
        public bool private_parent;
        public string created_at;
        public object created_by;

        public bool populated = false;

        public List<ProcoreFolder> GetFoldersRecursive() {
            if (this.populated) {
                return GetFoldersRecursive(this);
            }
            else {
                throw new Exception("Folder has not been populated");
            }
        }

        private List<ProcoreFolder> GetFoldersRecursive(ProcoreFolder folder) {
            var folders = new List<ProcoreFolder>();
            folders.AddRange(folder.folders);

            for (int i = 0; i < folder.folders.Length; i++) {
                var subfolder = folder.folders[i];
                folders.AddRange(GetFoldersRecursive(subfolder));
            }

            return folders;
        }

        public List<ProcoreFile> GetFilesRecursive() {
            if (this.populated) {
                return GetFilesRecursive(this);
            }
            else {
                throw new Exception("Folder has not been populated");
            }
        }

        private List<ProcoreFile> GetFilesRecursive(ProcoreFolder folder) {
            var files = new List<ProcoreFile>();
            files.AddRange(folder.files);

            for (int i = 0; i < folder.folders.Length; i++) {
                var subfolder = folder.folders[i];
                files.AddRange(GetFilesRecursive(subfolder));
            }

            return files;
        }

        public List<IFolderContent> Contents { 
            get {
                var contents = new List<IFolderContent>();
                contents.AddRange(folders);
                contents.AddRange(files);

                return contents;
            }
        }
        
        public IProject Project { get; set; }

        public string Name => name;

        public string Id => id.ToString();

        public DateTime DateModified =>
            DateTime.ParseExact(updated_at,
            "yyyy-MM-ddTHH:mm:ssZ",
            System.Globalization.CultureInfo.CurrentCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal) // parse as UTC time
            .ToLocalTime(); // convert to local time;

        public ContentType ContentType => ContentType.Folder;
    }

    public class ProcoreFile : IFile {
        public int? id;
        public string name;
        public int? parent_id;
        public int? size;
        public string description;
        public string updated_at;
        public string created_at;
        public string name_with_path;
        public ProcoreFileVersion[] file_versions;
        public string file_type;
        public bool? is_deleted;
        public object checked_out_by;
        public string checked_out_until;
        public bool? is_tracked;
        public ProcoreFolder tracked_folder;
        public bool? @private;
        public string legacy_id;
        public bool? private_parent;
        public object created_by;
        public bool? read_only;
        public string viewable_url;

        public int Version => file_versions.First().number ?? -1;

        public IProject Project { get; set; }

        public string Name => name;

        public string Id => id.ToString();

        public DateTime DateModified => 
            DateTime.ParseExact(updated_at,
            "yyyy-MM-ddTHH:mm:ssZ",
            System.Globalization.CultureInfo.CurrentCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal) // parse as UTC time
            .ToLocalTime(); // convert to local time;

        public ContentType ContentType => ContentType.File;

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
    }

    public class ProcoreFileVersion {
        public int? id;
        public string notes;
        public string url;
        public int? size;
        public string created_at;
        public int? number;
        public object created_by;
        public int? file_id;
        public object prostore_file;
    }
}
