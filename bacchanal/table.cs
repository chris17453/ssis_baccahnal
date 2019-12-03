using System;
using System.Collections.Generic;
using System.Text;

namespace bacchanal {
    class table_translator {

        /**************************************************************
        ROW = deleniated by end of line character "\n" +[\r]?
        delimiter = ':';
        comment='#'

        Row contains:
                array of columns

        columns contain :
            data_item
            comma seperated array of data_items

        data_item:
            single value
            key value store {x} = {y}
        **************************************************************/

        //bob:sam:bob,sam,dave:bob,sam=GenericUriParserOptions,dave=pop

        public class table {
            public List<row> items = new List<row>();
            public bool derive_key_columns = true;
            public bool derive_array_columns = true;
            public table_config config;

            public table(string file_path, table_config config) {
                this.config = config;
                string[] rows = System.IO.File.ReadAllLines(file_path);
                int index = 0;
                foreach (string row in rows) {
                    row processed_row = new row(row, config, index);
                    items.Add(processed_row);
                    ++index;
                }

                populate_min_max();
                derive_columns();
                derive_array();
                derive_column_data();

            }//end constructor
            public void populate_min_max() {
                //pre populate min/max counts
                foreach (row r in this.items) {
                    foreach (column c in r.items) {
                        column_definition column_def = this.config.columns.get_column_by_ordinal(c.index);
                        if (column_def == null) {
                            //error
                        }
                        if (column_def.max_items < c.items.Count) column_def.max_items = c.items.Count;
                        if (column_def.min_items > c.items.Count) column_def.min_items = c.items.Count;
                    }//end minor
                }//end major loop
            }

            public void derive_columns() {
                //create columns based on key data 
                if (this.derive_key_columns) {
                    foreach (row r in this.items) {
                        foreach (column c in r.items) {
                            column_definition column_def = this.config.columns.get_column_by_ordinal(c.index);
                            foreach (data_item d in c.items) {
                                if (d.is_key_value) {
                                    this.config.columns.AddDerived(d.key);
                                }
                            }//end inner
                        }//end minor
                    }//end major loop
                }//end creating derived key columns

            }
            public void derive_array() {
                //create columns based on array index data of a column
                if (this.derive_array_columns) {
                    foreach (row r in this.items) {
                        foreach (column c in r.items) {
                            column_definition column_def = this.config.columns.get_column_by_ordinal(c.index);
                            //if there is more than oone of them....
                            if (column_def.max_items > 1) {
                                for (int a = 0; a < column_def.max_items + 1; a++) {
                                    this.config.columns.AddDerived(string.Format("{0}.{1}", column_def.name, a));
                                }
                            }
                        }
                    }
                }
            }

            //populate derived column data after the fact.
            public void derive_column_data() {

                if (this.derive_key_columns | this.derive_array_columns) {
                    foreach (row r in this.items) {
                        int count = r.items.Count;
                            for (int i=0;i<count;i++){
                            column c = r.items[i];
                            column_definition column_def = this.config.columns.get_column_by_ordinal(c.index);
                            List<data_item> new_data = new List<data_item>();
                            
                            foreach (data_item d in c.items) {
                                if (d.is_key_value) {
                                    column_definition derived_column_def =this.config.columns.get_column_by_name(d.key);
                                    r.AddDerivedData(d, derived_column_def.ordinal);
                                    continue;
                                } 

                                if (d.is_value) {
                                    //we only add array data if the max item count is >1
                                    if (column_def.max_items > 1) {
                                        column_definition derived_column_def=this.config.columns.get_column_by_ordinal(c.index,d.index);
                                        r.AddDerivedData(d, derived_column_def.ordinal);
                                    }
                                }
                            }//end inner
                            
                        }//end minor
                    }//end major loop

                }//end if derived
            }//end 
        }//end table class

        public class table_config {
            public char comment_delimiter;
            public char column_delimiter;
            public char array_delimiter;
            public char key_value_delimiter;
            public columns_definition columns = new columns_definition();
            public table_config(char comment_delimiter = '#', char column_delimiter = ',', char array_delimiter = '|', char key_value_delimiter = '=') {
                this.comment_delimiter = comment_delimiter;
                this.column_delimiter = column_delimiter;
                this.array_delimiter = array_delimiter;
                this.key_value_delimiter = key_value_delimiter;
            }
        }

        public class row {
            public List<column> items = new List<column>();
            public int index=0;
            public bool error = false;
            public table_config config;
            public row(string data, table_config config,int index) {
                this.config = config;
                //clean data by trimming
                this.index = index;
                string clean_row = data.Trim();
                

                //if its empty skip. we dont need it
                if (clean_row.Length == 0) return;

                //comments are skipped...
                if (clean_row[0] == config.comment_delimiter) return;


                //process whats left into columns
                //split the ros on the column delimiter
                string[] raw_colums = data.Split(config.column_delimiter);

                int column_index = 0;
                //loop through the columns and append them
                foreach (string raw_column in raw_colums) {
                    column processed_column = new column(raw_column, config,column_index);
                    //add proccessed row, with or without errors
                    items.Add(processed_column);
                    ++column_index;
                }//end column loop

            }//end constructor
            public bool AddDerivedData(data_item data,int ordinal) {
                foreach(column c in this.items) {
                    //we cant update an ordinal that already exists...
                    if (c.index == ordinal) {
                        return false;
                    }
                }

                //ok we got herer, the column doesnt exist. lets add it.
                column new_data_column = new column(data.value,this.config, ordinal);
                this.items.Add(new_data_column);
                
                return true;
            }
        }//end row class

        public class column {
            public List<data_item> items = new List<data_item>();
            public int index=0;
            public bool error = false;
            public table_config config;


             public column(string data, table_config config,int index) {
                this.index = index;
                this.config = config;
                //this.items = new items();
                string[] column_elements = data.Split(config.array_delimiter);

                //loop through the elements
                int element_index = 0;
                column_definition column_def = config.columns.get_column_by_ordinal(index);
                if(column_def==null) {
                    // if we have no column. its because it wasnt defined. so add a default name based on index
                    // then set it to derived and arry type, the least restrictive
                    bool new_col_results=config.columns.Add("COLUMN_" + index,index,index,2,true);
                    if (false== new_col_results) {
                        //error
                    }
                    
                    column_def = config.columns.get_column_by_ordinal(index);
                    if (column_def == null) {
                        //cant add it at all error
                    }

                }
                if (column_def.type == 1 && column_elements.Length > 1) this.error = true;

                foreach (string element in column_elements) {
                    //process the data_item
                    data_item new_item = new data_item(element, config, element_index) ;
                    items.Add(new_item);
                    if (new_item.error == true) this.error = true;
                }//end elements loop
            }
        }//end column class

        public class data_item {
            public string key;
            public string value;
            public bool is_value = false;
            public bool is_key_value = false;
            public bool error = false;
            public int index = 0;

            public data_item(string data, table_config config,int index) {
                this.index = index;
                string[] tokens = data.Split(config.key_value_delimiter, 2);

                //is it a key value?, there will never be an option other than 0,1,2.
                switch (tokens.Length) {
                    case 0:
                        this.error = true;                               //error nothing?
                        break;
                    case 1:
                        this.value = tokens[0].Trim();                          //must not be a key value. so its just a value
                        this.is_value = true;
                        break;
                    case 2:
                        this.key = tokens[0].Trim();                            // key value
                        this.value = tokens[1].Trim();
                        this.is_key_value = true;
                        break;

                }
            }//end constructor
        }//end data item class

        int COLUMN_TYPE_STRING = 1;
        int COLUMN_TYPE_ARRAY = 2;
        public class column_definition {
            public string name;
            public string internal_name;
            public int ordinal = -1;
            public int array_index=0;
            public int order = -1;
            public int type = 1;
            public bool derived = false;
            public int max_items = 0;
            public int min_items = 0;
            public bool active= true;
            public column_definition(string name, int ordinal, int order, int type, bool derived=false, int array_index = 0) {
                this.name = name;
                this.ordinal = ordinal;
                this.order = order;
                this.type = type;
                this.derived = derived;
                this.internal_name = string.Format("{0}.{1}", ordinal, array_index);
                this.array_index = array_index;
            }
        }

        public class columns_definition {
            public List<column_definition> items = new List<column_definition>();
            public columns_definition() {

            }


            public column_definition get_column_by_name(string name) {
                foreach (column_definition c in this.items) {
                        if (name.ToLower() == c.name.ToLower()) return c;
                }
                return null;
            }
            public column_definition get_column_by_ordinal(int ordinal,int array_index=0) {
                foreach (column_definition c in this.items) {
                    if (c.ordinal == ordinal && c.array_index==array_index) return c;
                }
                return null;
            }

            //add a column thats computed
            public bool AddDerived(string name) {
                return this.Add(name,true);
            }

            //ad a static column, auto create ordinal,order,type
            public bool Add(string name) {
                return this.Add(name, false);
            }
            
            //add a column by staric or derived
            public bool Add(string name,bool derived=false) {
                int ordinal= -1;
                int order = -1;
                int type = 1;

                foreach (column_definition c in items) {
                    if (c.name.ToLower() == name.ToLower()) return false;
                    if (c.ordinal == ordinal) return false;
                    if (c.order == order) return false;
                }
                ordinal = items.Count;
                order = items.Count;
                return this.Add(name, ordinal, order, type,derived);
            }

            //underlying add column function base
            public bool Add(string name,int ordinal,int order,int type,bool derived) {
                //if it already exist, is the same ordinal or order... dont add it.
                foreach (column_definition c in items) {
                    if (c.name.ToLower() == name.ToLower()) return false;
                    if (c.ordinal == ordinal) return false;
                    if (c.order == order) return false;
                }
                column_definition new_column = new column_definition(name, ordinal, order, type,derived);
                new_column.min_items = 0;
                new_column.max_items = 1;
                items.Add(new_column);
                return true;
            }
        }
    
        public table_translator(string file_path) {


            // after all the column definitions are created
            // static data is populated
            // then derived columns are generated if desired
            // finally the derived data is populated from the standard data


            table_config config = new table_config('#', ':', ',', '=');
            //config.columns.Add("node"        ); 
            //config.columns.Add("hostname"    );
            //config.columns.Add("interface"   );
            //config.columns.Add("ip"          );
            //config.columns.Add("network"     );
            //config.columns.Add("defaultroute");
            //config.columns.Add("routeset"    );
            //config.columns.Add("MAC"         );
            //config.columns.Add("link_setting");

            table table = new table(file_path, config);




           //display columns 
            foreach (column_definition cd in config.columns.items) {
                string line = string.Format("Name: {0} Ordinal: {1} Derived: {2} min_items: {3} max_items: {4}", cd.name, cd.ordinal, cd.derived, cd.min_items, cd.max_items);
                Console.WriteLine(line);
            }

    
            foreach(row r in table.items) {
                string o = "";
                foreach (column c in r.items) {
                    foreach(data_item d in c.items) {
                        o += string.Format("{0}.{1}={2},", c.index, d.index, d.value);
                    }
                }
                Console.WriteLine(o);
            }

        }
    }//end class table_translator
}//end namespace
