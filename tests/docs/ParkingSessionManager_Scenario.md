These test `ParkingSessionManager` using **Moq test doubles** (all deps are mocked).

| #   | Scenario                                    | Test Double Setup                                      | Expected Behavior                           | Verifications                                              |
| --- | ------------------------------------------- | ------------------------------------------------------ | ------------------------------------------- | ---------------------------------------------------------- |
| 1   | CheckIn happy path                          | Repo returns null (no active ticket); DateTime stubbed | Returns new ticket with correct plate       | `SaveTicketAsync` called once                              |
| 2   | Duplicate check-in                          | Repo returns an existing active ticket                 | Throws `InvalidOperationException`          | `SaveTicketAsync` NOT called                               |
| 3   | CheckOut happy path                         | Ticket found, not checked out; Payment succeeds        | Returns `ParkingFeeResult` with correct fee | Payment called once, receipt sent once                     |
| 4   | Payment failure                             | Payment gateway throws exception                       | Exception propagates; ticket NOT updated    | `UpdateTicketAsync` NOT called                             |
| 5   | Notification failure (graceful degradation) | Payment succeeds; notification throws                  | Checkout still returns result               | `UpdateTicketAsync` IS called despite notification failure |
| 6   | Ticket not found on checkout                | Repo returns null for ticket ID                        | Throws `KeyNotFoundException`               | No payment, no update                                      |
| 7   | Already checked-out ticket                  | Ticket found but `IsActive == false`                   | Throws `InvalidOperationException`          | No payment                                                 |
