using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes.NAS
{
    public class CNasFileDataVmc
    {
        [Index(0)]
        public string _512kb_2mb { get; set; }
        [Index(1)]
        public string _128_512kb { get; set; }
        [Index(2)]
        public string _50_100_subdirs { get; set; }
        [Index(3)]
        public string _50_100_files { get; set; }
        [Index(4)]
        public string _200_500_subdirs { get; set; }
        [Index(5)]
        public string _1_5_files { get; set; }
        [Index(6)]
        public string DirsCountsByNumberOfFiles { get; set; }
        [Index(7)]
        public string _100_200_files { get; set; }
        [Index(8)]
        public string _64_128kb { get; set; }
        [Index(9)]
        public string NasBackupSourceShareStatsJobID { get; set; }
        [Index(10)]
        public string _1m_plus_files { get; set; }
        [Index(11)]
        public string _128_256mb { get; set; }
        [Index(12)]
        public string _1_64gb { get; set; }
        [Index(13)]
        public string _10k_100k_files { get; set; }
        [Index(14)]
        public string _5_10_subdirs { get; set; }
        [Index(15)]
        public string _1k_10k_files { get; set; }
        [Index(16)]
        public string FilesCountsBySizes { get; set; }
        [Index(17)]
        public string _1tb_plus { get; set; }
        [Index(18)]
        public string _10_25_subdirs { get; set; }
        [Index(19)]
        public string _64_128mb { get; set; }
        [Index(20)]
        public string _512gb_1tb { get; set; }
        [Index(21)]
        public string _64_512gb { get; set; }
        [Index(22)]
        public string _100k_1m_subdirs { get; set; }
        [Index(23)]
        public string _25_50_subdirs { get; set; }
        [Index(24)]
        public string _1_5_subdirs { get; set; }
        [Index(25)]
        public string _16_64mb { get; set; }
        [Index(26)]
        public string _100k_1m_files { get; set; }
        [Index(27)]
        public string _0_1_files { get; set; }
        [Index(28)]
        public string _500_1k_files { get; set; }
        [Index(29)]
        public string _500_1k_subdirs { get; set; }
        [Index(30)]
        public string _25_50_files { get; set; }
        [Index(31)]
        public string DirsCountByNumberOfSubDirs { get; set; }
        [Index(32)]
        public string _0_1_subdirs { get; set; }
        [Index(33)]
        public string _10k_100k_subdirs { get; set; }
        [Index(34)]
        public string _1k_10k_subdirs { get; set; }
        [Index(35)]
        public string TotalFileSizes { get; set; }
        [Index(36)]
        public string _2_16mb { get; set; }
        [Index(37)]
        public string ShareID { get; set; }
        [Index(38)]
        public string _5_10_files { get; set; }
        [Index(39)]
        public string _100_200_subdirs { get; set; }
        [Index(40)]
        public string _256mb_1gb { get; set; }
        [Index(41)]
        public string _4_16kb { get; set; }
        [Index(42)]
        public string _10_25_files { get; set; }
        [Index(43)]
        public string _1m_plus_subdirs { get; set; }
        [Index(44)]
        public string _200_500_files { get; set; }
        [Index(45)]
        public string _16_64kb { get; set; }
        [Index(46)]
        public string _0_4kb { get; set; }
    }
}
