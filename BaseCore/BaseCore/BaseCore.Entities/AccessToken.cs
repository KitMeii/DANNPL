
using BaseCore.Common;
using System;
using System.Collections.Generic;

namespace BaseCore.Entities
{
    public partial class AccessToken: Entity
    {
        public Guid Guid { get; set; }
       
        public string UserId { get; set; }
        public string Token { get; set; }
        public DateTime Expirated { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public virtual ICollection<Role> Roles { get; set; } 
        public virtual User User { get; set; }
    }
}
