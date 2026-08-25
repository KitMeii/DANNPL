
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseCore.DTO.AuthPlatform
{
    public class FunctionDto
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsChecked { get; set; } = false;
    }
}
