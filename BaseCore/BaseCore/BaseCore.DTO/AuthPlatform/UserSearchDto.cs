
using BaseCore.DTO.Common;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class UserSearchDto: Paging
    {
        public SortColumn SortColumn { get; set; }
        public InsertUserParam Data { get; set; }
    }

    public class UserSearchResult : SearchResult
    {
        public List<UserDto> Records { get; set; }
    }
}
