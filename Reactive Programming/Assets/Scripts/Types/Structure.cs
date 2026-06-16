using UnityEngine;

namespace Types.Enums {
     public class Structure : MonoBehaviour, IStructure {
        [SerializeField] private StructureType _type;

        public StructureType Type => _type;
         
     }
}