using System.Collections.Generic;

namespace QF.BySoft.Entities.Repositories;

public interface IMaterialMappingRepository
{
    string GetMaterialCodeFromKeywords(IEnumerable<string> keywords);

    string GetMaterialCodeFromArticle(string materialId);
}
