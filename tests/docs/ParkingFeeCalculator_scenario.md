| #   | Scenario                                        | Vehicle    | Duration       | Membership | Special | Expected TotalFee            | Technique |
| --- | ----------------------------------------------- | ---------- | -------------- | ---------- | ------- | ---------------------------- | --------- |
| 1   | Basic motorcycle hourly fee                     | Motorcycle | 2h             | Guest      | None    | 1,000 KHR                    | Unit Test |
| 2   | Basic car hourly fee                            | Car        | 3h             | Guest      | None    | 3,000 KHR                    | Unit Test |
| 3   | Basic SUV hourly fee                            | SUV        | 1h             | Guest      | None    | 1,500 KHR                    | Unit Test |
| 4   | Grace period — exactly 30 min                   | Car        | 30 min         | Guest      | None    | 0 KHR                        | Unit Test |
| 5   | Grace period — 29 min (under)                   | Car        | 29 min         | Guest      | None    | 0 KHR                        | Unit Test |
| 6   | Just past grace — 31 min                        | Car        | 31 min         | Guest      | None    | 1,000 KHR                    | Unit Test |
| 7   | Duration rounding — 1h 1min past grace          | Car        | 1h 31min total | Guest      | None    | 2,000 KHR                    | Unit Test |
| 8   | Daily cap — motorcycle 10h                      | Motorcycle | 10h            | Guest      | None    | 4,000 KHR                    | Unit Test |
| 9   | Daily cap — car 12h                             | Car        | 12h            | Guest      | None    | 8,000 KHR                    | Unit Test |
| 10  | Overnight fee — spans past 10 PM                | Car        | 8PM→11PM       | Guest      | None    | 3,000 + 2,000 = 5,000 KHR    | Unit Test |
| 11  | Weekend surcharge — Saturday                    | Car        | 2h Saturday    | Guest      | None    | 2,400 KHR                    | Unit Test |
| 12  | Holiday surcharge — overrides weekend           | Car        | 2h Saturday    | Guest      | Holiday | 3,000 KHR                    | Unit Test |
| 13  | Silver member discount                          | Car        | 2h             | Silver     | None    | 1,800 KHR                    | Unit Test |
| 14  | Gold member discount                            | Car        | 2h             | Gold       | None    | 1,500 KHR                    | Unit Test |
| 15  | Platinum member discount                        | Car        | 2h             | Platinum   | None    | 1,200 KHR                    | Unit Test |
| 16  | Lost ticket penalty                             | Car        | 2h             | Guest      | Lost    | 2,000 + 20,000 = 22,000 KHR  | Unit Test |
| 17  | Lost ticket during grace                        | Car        | 15 min         | Guest      | Lost    | 0 + 20,000 = 20,000 KHR      | Unit Test |
| 18  | checkOut before checkIn                         | Car        | invalid        | Guest      | None    | `ArgumentException`          | Unit Test |
| 19  | Zero duration (checkIn == checkOut)             | Car        | 0 min          | Guest      | None    | 0 KHR                        | Unit Test |
| 20  | Platinum + weekend (discount on base+surcharge) | Car        | 2h Saturday    | Platinum   | None    | (2,000+400)×0.60 = 1,440 KHR | Unit Test |

> ⚠️ Scenario 10 note: Car 8PM→11PM = 3 hours total.
> billableHours = ⌈(180-30)/60⌉ = ⌈2.5⌉ = 3 → baseFee = 3,000; overnight = +2,000 → total = 5,000 KHR
