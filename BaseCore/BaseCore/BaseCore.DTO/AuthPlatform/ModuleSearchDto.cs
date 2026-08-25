
using BaseCore.DTO.Common;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class ModuleSearchDto : Paging
    {
        public SortColumn SortColumn { get; set; }
        public ModuleModel Data { get; set; }
        public string RoleId { get; set; }
    }

    public class ModuleModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ModuleSearchResult : SearchResult
    {
        public List<ModuleDto> Records { get; set; }
    }
}
