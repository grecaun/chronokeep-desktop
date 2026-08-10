using Chronokeep.Objects.ChronoKeepAPI;
using System;
using System.Text;

namespace Chronokeep.Objects
{
    public class Participant : IEquatable<Participant>, IComparable<Participant>
    {
        private static string currentEventDate = "";
        private string birthdate;

        public Participant(
            string first, string last, string street, string city, string state, string zip,
            string birthday, EventSpecific epi, string email, string phone,
            string mobile, string parent, string country, string street2, string gender,
            string ecName, string ecPhone
            )
        {
            birthdate = birthday;
            FirstName = first;
            LastName = last;
            Street = street;
            City = city;
            State = state;
            Zip = zip;
            EventSpecific = epi;
            Email = email;
            Phone = phone;
            Mobile = mobile;
            Parent = parent;
            Country = country;
            Street2 = street2;
            Gender = gender;
            EcName = ecName;
            EcPhone = ecPhone;
            Trim();
            //FormatData();
        }

        public Participant(
            int id, string first, string last, string street, string city, string state, string zip,
            string birthday, EventSpecific? epi, string email, string phone,
            string mobile, string parent, string country, string street2, string gender,
            string ecName, string ecPhone
            )
        {
            birthdate = birthday;
            Identifier = id;
            FirstName = first;
            LastName = last;
            Street = street;
            City = city;
            State = state;
            Zip = zip;
            EventSpecific = epi ?? EventSpecific.Blank();
            Email = email;
            Phone = phone;
            Mobile = mobile;
            Parent = parent;
            Country = country;
            Street2 = street2;
            Gender = gender;
            EcName = ecName;
            EcPhone = ecPhone;
            Trim();
        }

        public int Identifier { get; set; } = Constants.Timing.PARTICIPANT_DUMMYIDENTIFIER;
        public string Birthdate => GetBirthdateString();

        private string GetBirthdateString()
        {
            if (!DateTime.TryParse(birthdate, out DateTime bd)) return "";
            return bd.Year < (DateTime.Now.Year - 120) ? "" : birthdate;
        }

        internal EventSpecific EventSpecific { get; }

        internal void Trim()
        {
            birthdate = birthdate.Trim();
            FirstName = FirstName.Trim();
            LastName = LastName.Trim();
            Street = Street.Trim();
            Street2 = Street2.Trim();
            City = City.Trim();
            State = State.Trim();
            Zip = Zip.Trim();
            EventSpecific.Trim();
            Email = Email.Trim();
            Phone = Phone.Trim();
            Mobile = Mobile.Trim();
            Parent = Parent.Trim();
            Country = Country.Trim();
            Gender = Gender.Trim();
            EcName = EcName.Trim();
            EcPhone = EcPhone.Trim();
        }

        internal void FormatData(bool UseMaleFemale)
        {
            if (!string.IsNullOrEmpty(FirstName))
            {
                FirstName = CapitalizeFirst(FirstName);
            }
            if (!string.IsNullOrEmpty(LastName))
            {
                LastName = CapitalizeFirst(LastName);
            }
            if (!string.IsNullOrEmpty(City))
            {
                City = CapitalizeFirst(City);
            }
            if (!string.IsNullOrEmpty(Street))
            {
                string[] addressArray = Street.Split(',');
                switch (addressArray.Length)
                {
                    case 2 when Street2.Length == 0:
                        Street = addressArray[0];
                        Street2 = addressArray[1];
                        break;
                    case > 2:
                        Street = addressArray[0];
                        break;
                }
            }
            if (!string.IsNullOrEmpty(Country))
            {
                if (Country.Equals("USA", StringComparison.OrdinalIgnoreCase) || Country.Equals("United States of America", StringComparison.OrdinalIgnoreCase) || Country.Equals("United States", StringComparison.OrdinalIgnoreCase))
                {
                    Country = "US";
                }
                else if (Country.Equals("CAN", StringComparison.OrdinalIgnoreCase) || Country.Equals("Canad", StringComparison.OrdinalIgnoreCase) || Country.Equals("Canada", StringComparison.OrdinalIgnoreCase))
                {
                    Country = "CA";
                }
            }
            if (!string.IsNullOrEmpty(State))
            {
                if (State.Length > 2)
                {
                    if (State.Equals("Alabama", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AL";
                    }
                    else if (State.Equals("Alaska", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AK";
                    }
                    else if (State.Equals("Arizona", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AZ";
                    }
                    else if (State.Equals("Arkansas", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AR";
                    }
                    else if (State.Equals("California", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "CA";
                    }
                    else if (State.Equals("Colorado", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "CO";
                    }
                    else if (State.Equals("Connecticut", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "CT";
                    }
                    else if (State.Equals("Delaware", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "DE";
                    }
                    else if (State.Equals("Florida", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "FL";
                    }
                    else if (State.Equals("Georgia", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "GA";
                    }
                    else if (State.Equals("Hawaii", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "HI";
                    }
                    else if (State.Equals("Idaho", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "ID";
                    }
                    else if (State.Equals("Illinois", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "IL";
                    }
                    else if (State.Equals("Indiana", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "IN";
                    }
                    else if (State.Equals("Iowa", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "IA";
                    }
                    else if (State.Equals("Kansas", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "KS";
                    }
                    else if (State.Equals("Kentucky", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "KY";
                    }
                    else if (State.Equals("Louisianna", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "LA";
                    }
                    else if (State.Equals("Maine", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "ME";
                    }
                    else if (State.Equals("Maryland", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MD";
                    }
                    else if (State.Equals("Massachusetts", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MA";
                    }
                    else if (State.Equals("Michigan", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MI";
                    }
                    else if (State.Equals("Minnesota", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MN";
                    }
                    else if (State.Equals("Mississippi", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MS";
                    }
                    else if (State.Equals("Missouri", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MO";
                    }
                    else if (State.Equals("Montana", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MT";
                    }
                    else if (State.Equals("Nebraska", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "NE";
                    }
                    else if (State.Equals("Nevada", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "NV";
                    }
                    else if (State.Equals("New Hampshire", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "NH";
                    }
                    else if (State.Equals("New Jersey", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "NJ";
                    }
                    else if (State.Equals("New Mexico", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "NM";
                    }
                    else if (State.Equals("New York", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "NY";
                    }
                    else if (State.Equals("North Carolina", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "NC";
                    }
                    else if (State.Equals("North Dakota", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "ND";
                    }
                    else if (State.Equals("Ohio", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "OH";
                    }
                    else if (State.Equals("Oklahoma", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "OK";
                    }
                    else if (State.Equals("Oregon", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "OR";
                    }
                    else if (State.Equals("Pennsylvania", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "PA";
                    }
                    else if (State.Equals("Rhode Island", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "RI";
                    }
                    else if (State.Equals("South Carolina", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "SC";
                    }
                    else if (State.Equals("South Dakota", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "SD";
                    }
                    else if (State.Equals("Tennessee", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "TN";
                    }
                    else if (State.Equals("Texas", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "TX";
                    }
                    else if (State.Equals("Utah", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "UT";
                    }
                    else if (State.Equals("Vermont", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "VT";
                    }
                    else if (State.Equals("Virginia", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "VA";
                    }
                    else if (State.Equals("Washington", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "WA";
                    }
                    else if (State.Equals("West Virginia", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "WV";
                    }
                    else if (State.Equals("Wisconsin", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "WI";
                    }
                    else if (State.Equals("Wyoming", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "WY";
                    }
                    else if (State.Equals("American Samoa", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AS";
                    }
                    else if (State.Equals("District of Columbia", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "DC";
                    }
                    else if (State.Equals("Federated States of Micronesia", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "FM";
                    }
                    else if (State.Equals("Guam", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "GU";
                    }
                    else if (State.Equals("Marshall Islands", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MH";
                    }
                    else if (State.Equals("Northern Mariana Islands", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "MP";
                    }
                    else if (State.Equals("Puerto Rico", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "PR";
                    }
                    else if (State.Equals("Palau", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "PW";
                    }
                    else if (State.Equals("Virgin Islands", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "VI";
                    }
                    else if (State.Equals("Armed Forces Americas", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AA";
                    }
                    else if (State.Equals("Armed Forces Africa", StringComparison.OrdinalIgnoreCase) || State.Equals("Armed Forces Canada", StringComparison.OrdinalIgnoreCase) || State.Equals("Armed Forces Europe", StringComparison.OrdinalIgnoreCase) || State.Equals("Armed Forces Middle East", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AE";
                    }
                    else if (State.Equals("Armed Forces Pacific", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "AP";
                    }
                    else if (State.Equals("British Columbia", StringComparison.OrdinalIgnoreCase))
                    {
                        State = "BC";
                    }
                }
                else
                {
                    State = State.ToUpper();
                }
            }
            string tmpPhone;
            if (!string.IsNullOrEmpty(Phone))
            {
                tmpPhone = Phone.Replace("-", "").Replace("+", "").Replace("(", "").Replace(")", "").Replace(" ", "").Replace(",", "").Replace(".", "").Trim();
                Phone = tmpPhone.Length switch
                {
                    10 => $"{tmpPhone[..3]}-{tmpPhone.Substring(3, 3)}-{tmpPhone.Substring(6, 4)}",
                    11 => $"{tmpPhone[..1]}-{tmpPhone.Substring(1, 3)}-{tmpPhone.Substring(4, 3)}-{tmpPhone.Substring(7, 4)}",
                    _ => Phone
                };
            }
            if (!string.IsNullOrEmpty(Mobile))
            {
                tmpPhone = Mobile.Replace("-", "").Replace("+", "").Replace("(", "").Replace(")", "").Replace(" ", "").Replace(",", "").Replace(".", "").Trim();
                Mobile = tmpPhone.Length switch
                {
                    10 => $"{tmpPhone[..3]}-{tmpPhone.Substring(3, 3)}-{tmpPhone.Substring(6, 4)}",
                    11 => $"{tmpPhone[..1]}-{tmpPhone.Substring(1, 3)}-{tmpPhone.Substring(4, 3)}-{tmpPhone.Substring(7, 4)}",
                    _ => Mobile
                };
            }
            if (!string.IsNullOrEmpty(EcPhone))
            {
                tmpPhone = EcPhone.Replace("-", "").Replace("+", "").Replace("(", "").Replace(")", "").Replace(" ", "").Replace(",", "").Replace(".", "").Trim();
                EcPhone = tmpPhone.Length switch
                {
                    10 => $"{tmpPhone[..3]}-{tmpPhone.Substring(3, 3)}-{tmpPhone.Substring(6, 4)}",
                    11 => $"{tmpPhone[..1]}-{tmpPhone.Substring(1, 3)}-{tmpPhone.Substring(4, 3)}-{tmpPhone.Substring(7, 4)}",
                    _ => EcPhone
                };
            }
            if (!string.IsNullOrEmpty(Gender))
            {
                Gender = CapitalizeFirstAll(Gender.Trim());
                if (Gender.Equals("M", StringComparison.OrdinalIgnoreCase)
                    || Gender.Equals("Male", StringComparison.OrdinalIgnoreCase)
                    || Gender.Equals("Man", StringComparison.OrdinalIgnoreCase))
                {
                    Gender = UseMaleFemale ? "Male" : "Man";
                }
                else if (Gender.Equals("F", StringComparison.OrdinalIgnoreCase)
                    || Gender.Equals("Female", StringComparison.OrdinalIgnoreCase)
                    || Gender.Equals("W", StringComparison.OrdinalIgnoreCase)
                    || Gender.Equals("Woman", StringComparison.OrdinalIgnoreCase))
                {
                    Gender = UseMaleFemale ? "Female" : "Woman";
                }
                else if (Gender.Equals("NB", StringComparison.OrdinalIgnoreCase) ||
                    Gender.Equals("Non-Binary", StringComparison.OrdinalIgnoreCase) ||
                    Gender.Equals("non binary", StringComparison.OrdinalIgnoreCase) ||
                    Gender.Equals("nonbinary", StringComparison.OrdinalIgnoreCase) ||
                    Gender.Equals("X", StringComparison.OrdinalIgnoreCase))
                {
                    Gender = "Non-Binary";
                }
            }
            else
            {
                Gender = "Not Specified";
            }
            string dummyYear = $"{DateTime.Now.Year - 130}";
            if (!DateTime.TryParse(birthdate, out DateTime birthDateTime))
            {
                birthDateTime = DateTime.Parse($"{dummyYear}/01/01");
            }
            birthdate = birthDateTime.ToShortDateString();
        }

        private static bool AllCaps(string val) => val.Equals(val.ToUpper());

        private static string CapitalizeFirst(string val)
        {
            string outval = val;
            if (AllCaps(val))
            {
                outval = val.ToLower();
            }
            return outval.Length switch
            {
                < 1 => outval,
                1 => outval.ToUpper(),
                _ => string.Concat(outval[..1].ToUpper(), outval.AsSpan(1, outval.Length - 1))
            };
        }

        private static string CapitalizeFirstAll(string val)
        {
            string[] tmp = val.Split(' ');
            StringBuilder output = new();
            foreach (string s in tmp)
            {
                output.Append($"{CapitalizeFirst(s.Trim())} ");
            }
            return output.ToString().Trim();
        }

        // Event Specific binding stuffs
        public int EventIdentifier => EventSpecific.EventIdentifier;
        public string Bib => EventSpecific.Bib;
        public string Distance => EventSpecific.DistanceName;
        public string CheckedInStr => EventSpecific.CheckedIn == 0 ? "No" : "Yes";
        public bool IsCheckedIn => EventSpecific.CheckedIn == 1;
        public string Owes => EventSpecific.Owes;
        public string Other => EventSpecific.Other;
        public string Comments => EventSpecific.Comments;
        public int Status { get => EventSpecific.Status; set => EventSpecific.Status = value; }
        public string Apparel => EventSpecific.Apparel;

        // Emergency Contact binding stuffs
        public string EcName { get; private set; }
        public string EcPhone { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Street { get; private set; }
        public string City { get; private set; }
        public string State { get; private set; }
        public string Zip { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }
        public string Mobile { get; private set; }
        public string Parent { get; private set; }
        public string Country { get; private set; }
        public string Street2 { get; private set; }
        public string Gender { get; private set; }
        public bool Anonymous => EventSpecific.Anonymous;
        public string Division => EventSpecific.Division;

        public int CompareTo(Participant? other)
        {
            if (other == null) return 1;
            return EventSpecific.DistanceIdentifier == other.EventSpecific.DistanceIdentifier
                ? LastName == other.LastName
                    ? string.Compare(FirstName, other.FirstName, StringComparison.Ordinal)
                    : string.Compare(LastName, other.LastName, StringComparison.Ordinal)
                : string.Compare(EventSpecific.DistanceName, other.EventSpecific.DistanceName,
                    StringComparison.Ordinal);
        }

        public bool Equals(Participant? other)
        {
            if (other == null) return false;
            return Identifier == other.Identifier
                && Bib == other.Bib
                && EventSpecific.Identifier == other.EventSpecific.Identifier
                && FirstName.Equals(other.FirstName, StringComparison.OrdinalIgnoreCase)
                && LastName.Equals(other.LastName, StringComparison.OrdinalIgnoreCase)
                && Street.Equals(other.Street, StringComparison.OrdinalIgnoreCase)
                && Zip.Equals(other.Zip, StringComparison.OrdinalIgnoreCase)
                && Birthdate.Equals(other.Birthdate, StringComparison.OrdinalIgnoreCase);
        }

        public bool Is(Participant? other)
        {
            if (other == null) return false;
            return FirstName.Equals(other.FirstName, StringComparison.OrdinalIgnoreCase)
                && LastName.Equals(other.LastName, StringComparison.OrdinalIgnoreCase)
                && Street.Equals(other.Street, StringComparison.OrdinalIgnoreCase)
                && Zip.Equals(other.Zip, StringComparison.OrdinalIgnoreCase)
                && Birthdate.Equals(other.Birthdate, StringComparison.OrdinalIgnoreCase);
        }

        public bool Matches(Participant? other)
        {
            if (other == null) return false;
            return Identifier == other.Identifier
                && EventSpecific.Equals(other.EventSpecific)
                && FirstName.Equals(other.FirstName, StringComparison.OrdinalIgnoreCase)
                && LastName.Equals(other.LastName, StringComparison.OrdinalIgnoreCase)
                && Street.Equals(other.Street, StringComparison.OrdinalIgnoreCase)
                && Zip.Equals(other.Zip, StringComparison.OrdinalIgnoreCase)
                && Birthdate.Equals(other.Birthdate, StringComparison.OrdinalIgnoreCase);
        }

        public string Age(string eventDate)
        {
            if (string.IsNullOrEmpty(birthdate))
            {
                return "";
            }
            DateTime eventDateTime = Convert.ToDateTime(eventDate);
            DateTime myDateTime = Convert.ToDateTime(birthdate);
            int numYears = eventDateTime.Year - myDateTime.Year;
            if (eventDateTime.Month < myDateTime.Month || eventDateTime.Month == myDateTime.Month && eventDateTime.Day < myDateTime.Day)
            {
                numYears--;
            }
            return numYears > 120 ? "" : Convert.ToString(numYears);
        }

        public int GetAge(string eventDate)
        {
            if (string.IsNullOrEmpty(birthdate))
            {
                return -1;
            }
            DateTime eventDateTime = Convert.ToDateTime(eventDate);
            DateTime myDateTime = Convert.ToDateTime(birthdate);
            int numYears = eventDateTime.Year - myDateTime.Year;
            if (eventDateTime.Month < myDateTime.Month || eventDateTime.Month == myDateTime.Month && eventDateTime.Day < myDateTime.Day)
            {
                numYears--;
            }
            if (numYears > 120)
            {
                return -1;
            }
            return numYears;
        }

        public static int CompareByDistance(Participant? one, Participant? two)
        {
            if (two == null || one == null) return 1;
            return one.CompareTo(two);
        }

        public static int CompareByBib(Participant? one, Participant? two)
        {
            if (two == null || one == null) return 1;
            if (int.TryParse(one.Bib, out int bibOne) && int.TryParse(two.Bib, out int bibTwo))
            {
                return bibOne.CompareTo(bibTwo);
            }
            return string.Compare(one.Bib, two.Bib, StringComparison.Ordinal);
        }

        public static int CompareByName(Participant? one, Participant? two)
        {
            if (two == null || one == null) return 1;
            return one.LastName == two.LastName ? string.Compare(one.FirstName, two.FirstName, StringComparison.Ordinal) : string.Compare(one.LastName, two.LastName, StringComparison.Ordinal);
        }

        public bool IsNotMatch(string value)
        {
            return !EventSpecific.Bib.Contains(value, StringComparison.OrdinalIgnoreCase)
                && !FirstName.Contains(value, StringComparison.OrdinalIgnoreCase)
                && !LastName.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        public string PrettyAnonymous => Anonymous ? "Yes" : "";

        public void Update(
            string firstName,
            string lastName,
            string gender,
            string birthDate,
            Distance d,
            string bib,
            bool smsEnabled,
            string mobile)
        {
            FirstName = firstName;
            LastName = lastName;
            Gender = gender;
            birthdate = birthDate;
            EventSpecific.DistanceIdentifier = d.Identifier;
            EventSpecific.DistanceName = d.Name;
            EventSpecific.Bib = bib;
            EventSpecific.SmsEnabled = smsEnabled;
            Mobile = mobile;
            Trim();
            //FormatData();
        }

        public void CopyFrom(Participant other)
        {
            EventSpecific.CopyFrom(other.EventSpecific);
            FirstName = other.FirstName;
            LastName = other.LastName;
            Gender = other.Gender;
            birthdate = other.Birthdate;
            Street = other.Street;
            City = other.City;
            State = other.State;
            Zip = other.Zip;
            Email = other.Email;
            Phone = other.Phone;
            Mobile = other.Mobile;
            Parent = other.Parent;
            Country = other.Country;
            Street2 = other.Street2;
            EcPhone = other.EcPhone;
            EcName = other.EcName;
            Trim();
            //FormatData();
        }

        public bool IsSimilar(ApiPerson other)
        {
            return FirstName.Equals(other.First, StringComparison.OrdinalIgnoreCase)
                || LastName.Equals(other.Last, StringComparison.OrdinalIgnoreCase)
                || (Gender.Equals(other.Gender, StringComparison.OrdinalIgnoreCase)
                && birthdate.Equals(other.Birthdate, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsSimilar(Registration.Participant other)
        {
            return FirstName.Equals(other.FirstName, StringComparison.OrdinalIgnoreCase)
                || LastName.Equals(other.LastName, StringComparison.OrdinalIgnoreCase)
                || (Gender.Equals(other.Gender, StringComparison.OrdinalIgnoreCase)
                && birthdate.Equals(other.Birthdate, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsSimilar(Participant other)
        {
            return FirstName.Equals(other.FirstName, StringComparison.OrdinalIgnoreCase)
                || LastName.Equals(other.LastName, StringComparison.OrdinalIgnoreCase)
                || (Gender.Equals(other.Gender, StringComparison.OrdinalIgnoreCase)
                && birthdate.Equals(other.birthdate, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsBasicMatch(Participant other)
        {
            return FirstName.Equals(other.FirstName, StringComparison.OrdinalIgnoreCase)
                && LastName.Equals(other.LastName, StringComparison.OrdinalIgnoreCase)
                && Gender.Equals(other.Gender, StringComparison.OrdinalIgnoreCase)
                && birthdate.Equals(other.birthdate, StringComparison.OrdinalIgnoreCase);
        }

        public string CurrentAge => Age(currentEventDate);

        public static void SetCurrentEventDate(string date)
        {
            currentEventDate = date;
        }
    }
}
