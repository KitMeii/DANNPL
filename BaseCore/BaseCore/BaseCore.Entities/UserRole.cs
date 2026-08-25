
using BaseCore.Common;
using BaseCore.Entities.Audit;
using System;
using System.Collections.Generic;

namespace BaseCore.Entities
{
    public partial class UserRole : Entity, IAuditable
    {
        public UserRole()
        {
           // Roles = new HashSet<Role>();
            //Users = new HashSet<User>();
        }

        public Guid Guid { get; set; }

    
        public string UserId { get; set; }
      
        public string RoleId { get; set; }
        public bool IsActive { get; set; }
        //public ObjectId? RoleUserId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDeleted { get; set; }
        public virtual User User { get; set; }
        public virtual Role Role { get; set; }
        //public virtual ICollection<Role> Roles { get; set; }
        //public virtual ICollection<User> Users { get; set; }
    }
}
