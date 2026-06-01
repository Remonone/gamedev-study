using Bus;
using UnityEngine;
using Utils.Attributes;

namespace Types.Events {
    [DoNotLog]
    [ErrorHandling(ErrorPolicy.SWALLOW)]
    [IgnoreErrorLog]
    public class ClickEvent : IEvent {
        public GameObject ClickedObject;

        public ClickEvent(GameObject clickedObject) {
            ClickedObject = clickedObject;
        }
    }
}