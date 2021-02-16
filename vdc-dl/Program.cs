using NLog;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.EntityFramework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VdcDl.Config;

namespace VdcDl {
    class Program {
        public static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static readonly AIS_DEV ais_dev = new AIS_DEV();

        static void Main(string[] args) {
            InitLogging();

            if (args.Length != 1) {
                Logger.Fatal("No project number argument provided");
                Environment.Exit(1);
            }

            var projectConfigs = LoadProjectConfigs();

            var projectConfig = projectConfigs.Single(x => x.projectNumber.ToString() == args[0]);
            Logger.Info($"Project number: {args[0]}");

            Debug.Assert(projectConfig.sources.Count > 0);
            Logger.Info($"{projectConfig.sources.Count} FTP source(s) found");

            foreach (var source in projectConfig.sources) {
                // login to project ftp
                IFtp ftp = FtpFactory.Create(source.ftpType);
                ftp.Login();

                List<ICompany> companies = ftp.GetCompanyListing();
                ICompany company = companies.Where(x => x.Id == source.companyId).Single();
                Logger.Info($"Found company");
                Logger.Info($"Name: {company.Name}");
                Logger.Info($"ID: {company.Id}");

                List<IProject> projects = ftp.GetProjectListing(company);
                IProject project = projects.Where(x => x.Id == source.projectId).Single();
                Logger.Info($"Found project");
                Logger.Info($"Name: {project.Name}");
                Logger.Info($"ID: {project.Id}");


                // get mappings from projectConfig
                List<Mapping> fileMappings = source.mappings.Where(x => x.mappingType == MappingType.FILE).ToList();
                List<Mapping> folderMappings = source.mappings.Where(x => x.mappingType == MappingType.FOLDER).ToList();

                Logger.Info($"{fileMappings.Count} file mappings");
                Logger.Info($"{folderMappings.Count} folder mappings");

                int filesAdded = 0;
                int filesUpdated = 0;
                int filesIgnored = 0;
                int filesAlreadyUpToDate = 0;


                void UpdateFtpFile(IFile ftpFile) {
                    // need to re-query files in database to avoid duplicates
                    // because new records aren't set by Entity Framework
                    var internalFileQuery = from file in ais_dev.VDC_FILES
                                            where file.FILE_PROJECT_NUMBER == projectConfig.projectNumber
                                            && file.FILE_EXTERNAL_NAME.ToUpper() == ftpFile.BaseName.ToUpper() // string.Equals() case insensitive doesnt work here
                                            && file.FILE_EXTENSION.ToUpper() == ftpFile.FileExt.ToUpper()
                                            select new {
                                                file.FILE_ACTION,
                                                file.FILE_EXTENSION,
                                                file.FILE_EXTERNAL_NAME,
                                                file.FILE_VERSION
                                            };

                    var internalFiles = internalFileQuery.ToList();


                    if (internalFiles.Count > 1) {
                        Logger.Error($"Duplicate external names found for {ftpFile.Name}");
                        throw new Exception($"Duplicate external names found for {ftpFile.Name}");
                    }


                    var internalFile = internalFiles.SingleOrDefault();

                    // if file not in database
                    if (internalFile == null) {
                        Logger.Warn($"File not in database");

                        ftp.DownloadFile(ftpFile, projectConfig.downloadDirectory);

                        InsertNewInternalFileRecord(ftpFile, projectConfig);

                        filesAdded++;
                        Logger.Info("New file added to database");
                    }

                    // else if no local file found
                    else if (!File.Exists(Path.Combine(projectConfig.downloadDirectory, ftpFile.Name))) {
                        Logger.Warn("No local file found, re-downloading");

                        ftp.DownloadFile(ftpFile, projectConfig.downloadDirectory);

                        UpdateInternalFileVersion(ftpFile, projectConfig.projectNumber);

                        filesUpdated++;
                        Logger.Info($"Re-downloaded file version {ftpFile.Version}");
                    }

                    // else if new version found on FTP
                    else if (ftpFile.Version > (internalFile.FILE_VERSION ?? 0)) {
                        Logger.Info("New version found");

                        ftp.DownloadFile(ftpFile, projectConfig.downloadDirectory);

                        UpdateInternalFileVersion(ftpFile, projectConfig.projectNumber);

                        filesUpdated++;
                        Logger.Info($"Updated internal file to version {ftpFile.Version}");
                    }

                    // else if internal file is more recent than FTP file (most likely duplicate filenames on FTP)
                    else if (ftpFile.Version < (internalFile.FILE_VERSION ?? 0)) {
                        filesIgnored++;
                        Logger.Warn("Internal file version greater than FTP file version, ignoring FTP file");
                    }

                    else {
                        filesAlreadyUpToDate++;
                        Logger.Info("File up to date");
                    }
                }


                // check and download single file mappings and merged models (glue only)
                foreach (var fileMapping in fileMappings) {
                    Logger.Info($"Processing mapping");
                    Logger.Info($"Type: FILE");
                    Logger.Info($"ID: {fileMapping.id}");

                    IFile ftpFile = ftp.GetFileInfo(project, fileMapping.id);

                    Logger.Info($"Name: {ftpFile.Name}");

                    UpdateFtpFile(ftpFile);
                }


                // check and download files in folder mappings
                foreach (var folderMapping in folderMappings) {
                    Logger.Info($"Processing mapping");
                    Logger.Info($"Type: FOLDER");
                    Logger.Info($"ID: {folderMapping.id}");

                    IFolder onlineFolder = ftp.GetFolderInfo(project, folderMapping.id);

                    Logger.Info($"Name: {onlineFolder.Name}");

                    List<IFile> filesInFolder = onlineFolder.Contents.OfType<IFile>().ToList();
                    Logger.Info($"{filesInFolder.Count} files in folder");


                    foreach (IFile ftpFile in filesInFolder) {
                        Logger.Info($"Processing mapping");
                        Logger.Info($"Type: FILE");
                        Logger.Info($"ID: {ftpFile.Id}");
                        Logger.Info($"Name: {ftpFile.Name}");

                        UpdateFtpFile(ftpFile);
                    }
                }


                // summary
                Logger.Info($"Processing of source complete");
                Logger.Info($"{filesAdded} files added");
                Logger.Info($"{filesUpdated} files updated");
                Logger.Info($"{filesIgnored} files ignored");
                Logger.Info($"{filesAlreadyUpToDate} files already up to date");
            }

            Logger.Info($"Processing of project complete");
        }

        private static void InitLogging() {
            var config = new NLog.Config.LoggingConfiguration();

            var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var logDirectory = Path.Combine(exeDirectory, "logs");
            Directory.CreateDirectory(logDirectory);

            var logfileName = DateTime.Now.ToString("yyyy-MM-dd(HHmm)") + ".txt";

            var logfilePath = Path.Combine(logDirectory, logfileName);

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = logfilePath };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Apply config           
            LogManager.Configuration = config;
        }

        private static void UpdateInternalFileVersion(IFile ftpFile, int projectNumber) {
            string conString = "; Data Source=ipaddress/prod;";
            //OracleConnection con = new OracleConnection(conString);
            using OracleConnection con = new OracleConnection(conString);
            OracleCommand cmd = con.CreateCommand();
            cmd.Parameters.Add("version", ftpFile.Version);
            cmd.Parameters.Add("basename", ftpFile.BaseName);
            cmd.Parameters.Add("ext", ftpFile.FileExt);
            cmd.Parameters.Add("projectNumber", projectNumber);

            cmd.CommandText = "update AIS_DEV.VDC_FILES " +
                              "set FILE_VERSION = :version, FILE_STATUS = 'Downloaded' " +
                              "where FILE_EXTERNAL_NAME = :basename " +
                              "and FILE_EXTENSION = :ext " +
                              "and FILE_PROJECT_NUMBER = :projectNumber";
            try {
                con.Open();
                int rowsAffected = cmd.ExecuteNonQuery();

                Debug.Assert(rowsAffected == 1);
            }
            catch {
                Logger.Error($"Error updating VDC_FILES table {ftpFile.BaseName}");
                throw new Exception($"Error updating VDC_FILES table {ftpFile.BaseName}");
            }
            finally {
                con.Close();
                con.Dispose();
            }
        }

        private static void InsertNewInternalFileRecord(IFile ftpFile, ProjectConfig projectConfig) {
            string conString = "redacted";

            //OracleConnection con = new OracleConnection(conString);
            using OracleConnection con = new OracleConnection(conString);
            con.Open();

            OracleCommand cmd = con.CreateCommand();
            cmd.Parameters.Add("projectNumber", projectConfig.projectNumber);
            cmd.Parameters.Add("basename", ftpFile.BaseName);
            cmd.Parameters.Add("ext", ftpFile.FileExt);
            cmd.Parameters.Add("version", ftpFile.Version);

            cmd.CommandText = "insert into AIS_DEV.VDC_FILES " +
                              "(FILE_PROJECT_NUMBER, FILE_LEVEL, FILE_TRADE_ABBREVIATION, FILE_EXTERNAL_NAME, FILE_EXTENSION, FILE_IS_PARENT, FILE_ACTION, FILE_VERSION, FILE_STATUS) " +
                              "values (:projectNumber, 'NOLEVEL', 'MISC', :basename, :ext, 0, 'COPY', :version, 'Downloaded')";
                try {
                    con.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    Debug.Assert(rowsAffected == 1);
                }
                catch {
                    Logger.Error($"Error inserting into VDC_FILES table {ftpFile.BaseName}");
                    throw new Exception($"Error inserting into VDC_FILES table {ftpFile.BaseName}");
                }
                finally {
                    con.Close();
                    con.Dispose();
                }
        
        }

        private static List<ProjectConfig> LoadProjectConfigs() {
            Logger.Info("Loading project configs");

            var fileMappingsquery = from r in ais_dev.VDC_FILE_MAPPINGS
                                    join n in ais_dev.VDC_PROJECTS on r.MAP_PROJECT_NUMBER equals n.PROJECT_NUMBER
                                    select new {
                                        r.MAP_PROJECT_NUMBER,
                                        n.PROJECT_NAME,
                                        r.MAP_FTP_SOURCE,
                                        r.MAP_COMPANY_ID,
                                        r.MAP_PROJECT_ID,
                                        r.MAP_TYPE,
                                        r.MAPPING_ID
                                    };

            var fileMappings = fileMappingsquery.ToList();


            var projectNumbers = fileMappings.Select(x => (int)x.MAP_PROJECT_NUMBER).Distinct();
            var projectConfigs = new List<ProjectConfig>();


            foreach (var projectNumber in projectNumbers) {
                var projectConfig = new ProjectConfig {
                    projectNumber = projectNumber,
                    projectName = fileMappings.First(x => x.MAP_PROJECT_NUMBER == projectNumber).PROJECT_NAME,
                    downloadDirectory = Path.Combine(@"\\cannfilesvr01.cannistraro.local\sys\data\everyone\VDC Group\_BIM Support\Turing\Downloads", 
                        $"{projectNumber} - {fileMappings.First(x => x.MAP_PROJECT_NUMBER == projectNumber).PROJECT_NAME}") // TODO remove cannistraro.local?
                };

                var configSourceNames = fileMappings.Where(x => x.MAP_PROJECT_NUMBER == projectNumber).Select(x => x.MAP_FTP_SOURCE).Distinct();


                foreach (var sourceName in configSourceNames) {
                    var sourceMappings = fileMappings.Where(x => x.MAP_PROJECT_NUMBER == projectNumber && x.MAP_FTP_SOURCE == sourceName);

                    var source = new Source {
                        ftpType = (FtpType)Enum.Parse(typeof(FtpType), sourceName),

                        // assume at most one companyId and projectId per source
                        // (max. one glue source per project, max. one procore source per project, etc.)
                        companyId = sourceMappings.First().MAP_COMPANY_ID,
                        projectId = sourceMappings.First().MAP_PROJECT_ID,

                        mappings = sourceMappings.Select(x =>
                            new Mapping {
                                id = x.MAPPING_ID,
                                mappingType = (MappingType)Enum.Parse(typeof(MappingType), x.MAP_TYPE)
                            }
                        ).ToList(),
                    };

                    projectConfig.sources.Add(source);
                }

                projectConfigs.Add(projectConfig);
            }

            return projectConfigs;
        }
    }

    // Entity Framework setup
    public sealed class EntityFrameworkConfiguration : DbConfiguration {
        public static readonly DbConfiguration Instance = new EntityFrameworkConfiguration();

        public EntityFrameworkConfiguration() {
            SetDefaultConnectionFactory(new OracleConnectionFactory());
            SetProviderServices("Oracle.ManagedDataAccess.Client", EFOracleProviderServices.Instance);
            SetProviderFactory("Oracle.ManagedDataAccess.Client", new OracleClientFactory());
        }
    }
}
