using UnityEngine;

namespace Types.Enums {
     public class Structure : MonoBehaviour, IStructure {
        [SerializeField] private GovernmentInteractionType _type;

        public GovernmentInteractionType Type => _type;
         
     }
}