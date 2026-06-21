using System.Threading;

namespace Chronokeep.Interfaces.UI
{
    public interface ISubPage : IMainPage
    {
        void CancelableUpdateView(CancellationToken token);
        void Search(CancellationToken token);
    }

    public enum PeopleType { KNOWN, ALL, STARTS, FINISHES, DEFAULT, UNKNOWN, UNKNOWN_FINISHES, UNKNOWN_STARTS }
    public enum SortType { SYSTIME, GUNTIME, BIB, DISTANCE, AGEGROUP, GENDER, PLACE }
}
