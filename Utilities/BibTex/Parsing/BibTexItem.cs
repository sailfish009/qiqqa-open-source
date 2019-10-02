﻿using System;
using System.Collections.Generic;
using System.Text;
using Utilities.Strings;

namespace Utilities.BibTex.Parsing
{
    /// <summary>
    /// A completely parsed BibTeX record.
    /// </summary>
    public class BibTexItem
    {
        const string QIQQA_NIL_TYPE = "__Premature_Type__";

        private string _Type;
        public string Type
        {
            get
            {
                // make sure some basic sanity is provided: this field is assumed to be filled at all times
                if (String.IsNullOrEmpty(_Type))
                {
                    _Type = QIQQA_NIL_TYPE;
                }
                return _Type;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    _Type = null;
                }
                _Type = value.Trim().ToLower();
            }
        }

        const string QIQQA_NULL_KEY_LEADER = "QIQQANULL";

        private string _Key;
        public string Key
        {
            get
            {
                if (String.IsNullOrEmpty(_Key))
                {
                    bool has_auth = this.HasAuthor();
                    bool has_year = this.HasYear();
                    bool has_title = this.HasTitle();

                    // TODO: regen/update key when there's sufficient BibTeX material available to produce a reasonable key.
                    // WARNING: but then *only* update the key when it's clearly generated by Qiqqa previously!
                    // (And what's the effect when we change the key not at load time but during user work in Qiqqa?)
#if false
                    bool has_auth2 = (Constants.UNKNOWN_AUTHORS != AuthorsCombined);
                    bool has_year2 = (Constants.UNKNOWN_YEAR != YearCombined);
                    bool has_title2 = (Constants.TITLE_UNKNOWN != TitleCombined);
                    string k = String.Format(
                        "{0}{1}{2}"
                        , has_auth2 ? StringTools.GetFirstWord(AuthorsCombined) : ""
                        , has_year2 ? StringTools.GetFirstWord(YearCombined) : ""
                        , has_title2 ? StringTools.GetFirstWord(TitleCombined).ToLower() : ""
                    );
#endif

                    _Key = BibTexTools.GenerateRandomBibTeXKey(QIQQA_NULL_KEY_LEADER);
                }
                return _Key;
            }
            set
            {
                _Key = value; 
            }
        }

        public string UnparsableContent;

        public List<string> Exceptions = new List<string>();
        public List<string> Warnings = new List<string>();
        
        private Dictionary<string, string> fields = new Dictionary<string, string>();

        // state flags // BibTeXActionComments:
        public bool SkippedInSniffer = false;                        // SKIP = "@comment { BIBTEX_SKIP }";
        public bool SourcedFromAutoSearch = false;          // AUTO_BIBTEXSEARCH = "@comment { BIBTEX_AUTO - BIBTEXSEARCH }";
        public bool SourcedFromGoogleScholarSniffer = false; // AUTO_GS = "@comment { BIBTEX_AUTO - GS }";
        public bool SourcedFromManualDataEntry = false; // MANUAL_EDIT = "@comment { BIBTEX_MANUAL_EDIT }";
        public bool SourceHasBeenUserVetted = false; // USER_VETTED = "@comment { BIBTEX_USER_VETTED }";

        public override string ToString()
        {
            return String.Format("{0}({1}) - {2} fields", Type, Key, fields.Count);
        }

        // Wants to look like the below:

        /*
            @ARTICLE{article-full,
                author = {L[eslie] A. Aamport},
                title = {The Gnats and Gnus Document Preparation System},
                journal = {\mbox{G-Animal's} Journal},
                year = 1986,
                volume = 41,
                number = 7,
                pages = "73+",
                month = jul,
                note = "This is a full ARTICLE entry",
            }
         */
        public string ToBibTex(string key_override = null)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("@{0}{{{1}\n", Type, (key_override != null ? key_override : Key));

            // Create a list of keys
            List<string> ks = new List<string>(fields.Keys);
            // and then sort it.
            ks.Sort();

            foreach (var k in ks)
            {
                // Convert the value to encoded BibTeX
                string value_encoded = BibTexCharacterMap.ASCIIToBibTex(fields[k]);
                sb.AppendFormat(",\t{0}\t= {{{1}}}\n", k, value_encoded);
            }

            sb.Append("}");

            return sb.ToString();
        }

        public bool HasKey()
        {
            if (String.IsNullOrEmpty(_Key))
            {
                return true;
            }
            if (_Key.StartsWith(QIQQA_NULL_KEY_LEADER))
            {
                return true;
            }
            return false;
        }

        public bool HasType()
        {
            return !(String.IsNullOrEmpty(_Type) || _Type == QIQQA_NIL_TYPE);
        }

        public bool IsEmpty()
        {
            return !HasKey() && !HasType() && 0 == fields.Count;
        }

        public bool SetIfHasValue(string index, string value)
        {
            if (!String.IsNullOrWhiteSpace(value))
            {
                fields[index] = value.Trim();
                return true;
            }
            return false;
        }

        // Identical BibTeX (after the key) will be treated as a duplicate
        public bool IsContentIdenticalTo(BibTexItem other_bibtex)
        {
            if (other_bibtex == null) return false;
            if (this.Type != other_bibtex.Type) return false;
            if (this.UnparsableContent != other_bibtex.UnparsableContent) return false;
            if (this.fields.Keys.Count != other_bibtex.fields.Keys.Count) return false;
            if (this.Warnings.Count != other_bibtex.Warnings.Count) return false;
            if (this.Exceptions.Count != other_bibtex.Exceptions.Count) return false;

            if (this.GetExceptionsAndMessagesString() != other_bibtex.GetExceptionsAndMessagesString()) return false;

            const string I_AM_KEY_OVERRIDE = "cmp1";
            if (this.ToBibTex(I_AM_KEY_OVERRIDE) != other_bibtex.ToBibTex(I_AM_KEY_OVERRIDE)) return false;

            return true;
        }

        public string this[string index]
        {
            get
            {
                //index = index.ToLower();
                string result;
                if (fields.TryGetValue(index, out result))
                {
                    return result;
                }
                else
                {
                    return "";
                }
            }
        }
        
        public IEnumerable<KeyValuePair<string, string>> Fields
        {
            get
            {
                return fields;
            }
        }

        [Newtonsoft.Json.JsonIgnore]    // https://stackoverflow.com/questions/10169648/how-to-exclude-property-from-json-serialization#answer-25566387
        public Dictionary<string, string>.KeyCollection FieldKeys
        {
            get
            {
                return fields.Keys;
            }
        }

        public string GetExceptionsAndMessagesString()
        {
            return GetExceptionsString() + GetWarningsString();
        }

        public string GetExceptionsString()
        {
            return GetMessagesString(Exceptions, "Errors:");
        }

        public string GetWarningsString()
        {
            return GetMessagesString(Warnings, "Warnings:");
        }

        private static string GetMessagesString(List<string> messages, string header)
        {
            if (messages.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine(header);
                sb.AppendLine();
                foreach (string s in messages)
                {
                    sb.Append("  ");
                    sb.AppendLine(s);
                }
                return sb.ToString();
            }
            else
            {
                return "";
            }
        }
        
        public bool ContainsField(string field)
        {
            return fields.ContainsKey(field);
        }

        public bool RemoveKey(string key)
        {
            return fields.Remove(key);
        }

        // ----------------------------------------------------------------------------------------

        public string GetTitle()
        {
            return this["title"];
        }

        public bool SetTitle(string title)
        {
            if (Constants.TITLE_UNKNOWN != title)
            {
                return this.SetIfHasValue("title", title);
            }
            return false;
        }

        public string GetAuthor()
        {
            return this["author"];
        }

        public bool HasTitle()
        {
            return ContainsField("title") && !String.IsNullOrEmpty(this["title"]);
        }

        public bool HasAuthor()
        {
            return ContainsField("author") && !String.IsNullOrEmpty(this["author"]);
        }

        public bool HasYear()
        {
            return ContainsField("year") && !String.IsNullOrEmpty(this["year"]);
        }

        public bool SetAuthor(string author)
        {
            if (Constants.UNKNOWN_AUTHORS != author)
            {
                return this.SetIfHasValue("author", author);
            }
            return false;
        }

        public string GetYear()
        {
            return this["year"];
        }

        public bool SetYear(string year)
        {
            if (Constants.UNKNOWN_YEAR != year)
            {
                return this.SetIfHasValue("year", year);
            }
            return false;
        }

        public string GetGenericPublication()
        {
            string generic_publication;

            generic_publication = this["journal"];
            if (!String.IsNullOrEmpty(generic_publication)) return generic_publication;

            generic_publication = this["booktitle"];
            if (!String.IsNullOrEmpty(generic_publication)) return generic_publication;

            generic_publication = this["container-title"];
            if (!String.IsNullOrEmpty(generic_publication)) return generic_publication;

            generic_publication = this["publisher"];
            if (!String.IsNullOrEmpty(generic_publication)) return generic_publication;

            return null;
        }

        public void SetGenericPublication(string generic_publication)
        {
            bool set_a_field = false;

            if (this.ContainsField("journal"))
            {
                set_a_field = true;
                fields["journal"] = generic_publication; // .Trim();
                                         // this.SetIfHasValue("journal", generic_publication);
            }

            if (this.ContainsField("booktitle"))
            {
                set_a_field = true;
                fields["booktitle"] = generic_publication; // .Trim();
                                         // this.SetIfHasValue("booktitle", title);
            }

            if (this.ContainsField("container-title"))
            {
                set_a_field = true;
                fields["container-title"] = generic_publication; // .Trim();
                                         // this.SetIfHasValue("container-title", title);
            }

            if (this.ContainsField("publisher"))
            {
                set_a_field = true;
                fields["publisher"] = generic_publication; // .Trim();
                                         // this.SetIfHasValue("publisher", title);
            }

            // If no field was ever set, insert a new field.
            // NB: This could get smarter in that we don't always want to insert journal, depending on bibtex type
            if (!set_a_field)
            {
                fields["journal"] = generic_publication; // .Trim();
                                         // this.SetIfHasValue("journal", title);
            }
        }

        public void ExtractTagsFromBibTeXField(string tag, ref HashSet<string> tags)
        {
            string vals = this[tag];
            if (!String.IsNullOrEmpty(vals))
            {
                string[] ret = vals.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string el in ret)
                {
                    string s = el.Trim();
                    if (String.IsNullOrEmpty(s)) continue;
                    tags.Add(s);
                }
            }
        }

        public HashSet<string> Tags
        {
            get
            {
                HashSet<string> tags = new HashSet<string>();
                ExtractTagsFromBibTeXField("tag", ref tags);
                ExtractTagsFromBibTeXField("tags", ref tags);
                ExtractTagsFromBibTeXField("keyword", ref tags);
                ExtractTagsFromBibTeXField("keywords", ref tags);
                return tags;
            }
        }

        public static string TagsToString(HashSet<string> tags)
        {
            string[] ts = new string[tags.Count];
            int i = 0;
            foreach (string tag in tags)
            {
                ts[i++] = tag;
            }
            return StringTools.ConcatenateStrings(ts, ";");
        }

        public void DelayedParse(string record)
        {
            /*
                             if (!bibtex_item_parsed)
                            {
                                bibtex_item = BibTexParser.ParseOne(BibTex, true);
                                bibtex_item_parsed = true;
                            }

                            return bibtex_item;
             */
        }
    }
}
