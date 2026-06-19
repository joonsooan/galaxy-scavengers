using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Tech Research Catalog", menuName = "Tech/Tech Research Catalog")]
public class TechResearchCatalog : ScriptableObject
{
    [SerializeField] private List<TechData> techs;

    public IReadOnlyList<TechData> Techs => techs;

    public TechData GetTechByIndex(int techIndex)
    {
        if (techs == null)
        {
            return null;
        }

        for (int i = 0; i < techs.Count; i++)
        {
            if (techs[i] != null && techs[i].techIndex == techIndex)
            {
                return techs[i];
            }
        }

        return null;
    }
}
