using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseCore.DTO.AuthPlatform
{
    public class UserRoleDto
    {

        [BsonRepresentation(BsonType.ObjectId)]
        public string  RoleId { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string  UserId { get; set; }
        public bool IsActive { get; set; }
    }
}
