
using BaseCore.DTO.Common;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class FunctionSearchDto : Paging
    {
        public SortColumn SortColumn { get; set; }
        public FunctionModel Data { get; set; }
    }

    public class FunctionModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool? IsActive { get; set; }
    }

    public class FunctionSearchResult : SearchResult
    {
        public List<FunctionDto> Records { get; set; }
    }
}
