using System;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class GroupRoleDto
    {
        public GroupRoleDto()
        {
            Permissions = new List<ModuleFuntionDto>();
        }
        public string Id { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public int UserCount { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsMember { get; set; }
        public bool? IsActive { get; set; }
        public string Name { get; set; }

        public List<ModuleFuntionDto> Permissions{ get; set; }
    }

    public class ModuleFuntionDto
    {
        public string Name { get; set; }
    }

    public class GroupRoleResultDto
    {
        public List<GroupRoleDto> GroupRoles { get; set; }
        public List<GroupRoleDto> GroupAccessColumns { get; set; }
    }
}
