using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace BaseCore.DTO.AuthPlatform
{
    public class UserDto
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Contact { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Fax { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string ShortName { get; set; }
        public string Position { get; set; }
        public string Image { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public int UserType { get; set; }
        public string Thumbnail { get; set; }
        public virtual ICollection<RoleDto> RoleUser { get; set; }

    }

    public class UserParam
    {
        public string Username { get; set; }
        //public string Email { get; set; }
        public string Password { get; set; }
    }

    public class UserInfo
    {
        public string ListRoles { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public int AreaNo { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public string Name { get; set; }
        public string AgencyId { get; set; }
        public string AgencyCode { get; set; }
        public string AgencyName { get; set; }
        public string AgencyAvatar { get; set; }
        public int UserType { get; set; }
        public string Avatar { get; set; }
    }

    public class InsertUserParam
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Contact { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Fax { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public string ShortName { get; set; }
        public string Position { get; set; }
        public string Image { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public int? UserType { get; set; }
        public int FilterType { get; set; }
        public string Thumbnail { get; set; }
        public ICollection<RoleDto> RoleUser { get; set; }

    }

    public class UpdateUsernameParam
    {
        public string Id { get; set; }
        public string OldUsername { get; set; }
        public string Password { get; set; }
        public string NewUsername { get; set; }
    }
}
