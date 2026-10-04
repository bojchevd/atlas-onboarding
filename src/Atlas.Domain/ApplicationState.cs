namespace Atlas.Domain;

public enum ApplicationState
{
    Draft,
    Submitted,
    Verifying,
    PendingManualReview,
    Approved,
    PendingBranchVisit,
    Activated,
    Rejected,
}
