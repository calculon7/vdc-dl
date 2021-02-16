using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VdcDl.Config;
using VdcDl.Glue;
using VdcDl.Procore;

namespace VdcDl {
    public interface IFtp {
        bool IsStaging { get; } // must be set before login
        bool IsLoggedIn { get; set; }

        void Login();

        List<ICompany> GetCompanyListing();

        List<IProject> GetProjectListing(ICompany company);

        List<IFile> GetFileListing(IProject project);

        IFolder GetFolderInfo(IProject project, string folderId);

        IFile GetFileInfo(IProject project, string fileId);

        FileInfo DownloadFile(IFile file, string downloadLocation);

        //List<IFolderContent> Search(IProject project, string searchTerm);
    }

    public static class FtpFactory {
        public static IFtp Create (FtpType ftpType) {
            Program.Logger.Info($"Initializing FTP of type {ftpType}");

            switch (ftpType) {
                case FtpType.GLUE:
                    return new GlueSession();

                case FtpType.PROCORE:
                    return new ProcoreSession();

                default:
                    throw new Exception();
            }
        }
    }

    public interface ICompany {
        IFtp Ftp { get; set; }
        string Name { get; }
        string Id { get; }
    }

    public interface IProject {
        ICompany Company { get; set; }
        string Name { get; }
        string Id { get; }
    }

    public enum ContentType {
        Folder,
        File
    }

    public interface IFolderContent {
        IProject Project { get; set; }
        string Name { get; }
        string Id { get; }
        DateTime DateModified { get; }
        ContentType ContentType { get; }
    }

    public interface IFolder : IFolderContent {
        List<IFolderContent> Contents { get; }
    }

    public interface IFile : IFolderContent {
        int Version { get; }
        string BaseName { get; }
        string FileExt { get; }
    }

    class GenericFolderContent : IFolderContent {
        public IProject Project { get; set; }

        public string Name { get; set; }

        public string Id { get; set; }

        public DateTime DateModified { get; set; }

        public ContentType ContentType { get; set; }
    }
}
