using System.Collections.Generic;

namespace QF.BySoft.Entities.Repositories;

public interface IMaterialMappingRepository
{
    string GetMaterialIdFromKeywords(IEnumerable<string> keywords);

    string GetMaterialIdFromArticle(string materialId);
}
