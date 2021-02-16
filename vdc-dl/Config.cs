using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VdcDl.Config {
    public enum FtpType {
        GLUE,
        PROCORE
    }

    public enum MappingType {
        FOLDER,
        FILE
    }

    class ProjectConfig {
        public int projectNumber;
        public string projectName;
        public string downloadDirectory;
        public List<Source> sources = new List<Source>();
    }

    class Source {
        public FtpType ftpType;
        public string companyId;
        public string projectId;
        public List<Mapping> mappings = new List<Mapping>();
    }

    class Mapping {
        public MappingType mappingType;
        public string id;
    }
}
