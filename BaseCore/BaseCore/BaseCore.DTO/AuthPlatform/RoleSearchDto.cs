
using BaseCore.DTO.Common;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class RoleSearchDto : Paging
    {
        public SortColumn SortColumn { get; set; }
        public RoleModel Data { get; set; }
    }

    public class RoleModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool? IsActive { get; set; }
    }

    public class RoleSearchResult : SearchResult
    {
        public List<RoleDto> Records { get; set; }
    }
}
