//
// System.Web.Mail.MailAddress.cs
//
// Author(s):
//   Per Arneng <pt99par@student.bth.se>
//
//

//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Text;

namespace FSpot.Mail {

    // Reperesents a mail address
    internal class MailAddress {
	
	protected string user;
	protected string host;
	protected string name;
	
	public string User {
	    get { return user; }
	    set { user = value; }
	}

	public string Host {
	    get { return host; }
	    set { host = value; }
	}

	public string Name {
	    get { return name; }
	    set { name = value; }
	}

	public string Address {
	    get { return String.Format( "{0}@{1}" , user , host ); }
	    set {
		
		string[] parts = value.Split( new char[] { '@' } );
		
		if( parts.Length != 2 ) 
		    throw new FormatException( "Invalid e-mail address: '" + value + "'.");
		
		user = parts[ 0 ];
		host = parts[ 1 ];
	    }
	}

	public static MailAddress Parse( string str ) {
	    if (str == null || str.Trim () == "")
	    	return null;

	    MailAddress addr = new MailAddress();
	    string address = null;
	    string nameString = null;
	    string[] parts = str.Split( new char[] { ' ' } );
	    
	    // find the address: xxx@xx.xxx
	    // and put to gether all the parts
	    // before the address as nameString
	    foreach( string part in parts ) {
		
		if( part.IndexOf( '@' ) > 0 ) {
		    address = part;
		    break;
		}
		
		nameString = nameString + part + " ";
	    }

	    if( address == null ) 
		throw new FormatException( "Invalid e-mail address: '" + str + "'.");
	    
	    address = address.Trim( new char[] { '<' , '>' , '(' , ')' } );
	    
	    addr.Address = address;
	    
	    if( nameString != null ) {
		addr.Name = nameString.Trim( new char[] { ' ' , '"' } );
		addr.Name = ( addr.Name.Length == 0 ? null : addr.Name ); 
	    }
	    
	    
	    return addr;
	} 
    
    
	public override string ToString() {
	    
	    string retString = "";
	
	    if( name == null ) {
		
		retString = String.Format( "<{0}>" , this.Address );
	    
	    } else {
		
		string personName = this.Name;

		if( MailUtil.NeedEncoding( personName ) ) {
		    personName = String.Format( "=?{0}?B?{1}?=",
					        Encoding.Default.BodyName , 
						MailUtil.Base64Encode( personName ) ) ;
		}

		retString = String.Format( "\"{0}\" <{1}>" , personName , this.Address);
	    
	    }
	    
	    return retString;
	}
    }

}
