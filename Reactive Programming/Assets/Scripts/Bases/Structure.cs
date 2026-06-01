using UnityEngine;
using Types;

namespace Bases {
     public class Structure : MonoBehaviour, IStructure {
        [SerializeField] private Types.StructureType _type;

        public Types.StructureType Type => _type;
         
     }
}