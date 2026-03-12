import os
import uuid
from flask import Flask, redirect, request, session, url_for
from dotenv import load_dotenv

# Load your Sorterra App values
load_dotenv()
CLIENT_ID = os.getenv("CLIENT_ID")
# We use the 'common' endpoint for multi-tenant apps during login
AUTHORITY = "https://login.microsoftonline.com/common"
REDIRECT_URI = "http://localhost:5000/callback"

app = Flask(__name__)
# Secret key for Flask session state
app.secret_key = str(uuid.uuid4())

@app.route("/")
def index():
    """The 'SaaS Dashboard' page"""
    html = """
    <h1>Welcome to Sorterra</h1>
    <p>To start sorting your files, connect your SharePoint account.</p>
    <a href="/login"><button style="padding: 10px 20px; font-size: 16px; background-color: #0078D4; color: white; border: none; cursor: pointer;">Connect to SharePoint</button></a>
    """
    return html

@app.route("/login")
def login():
    """Generates the Admin Consent URL and redirects the user"""
    # Generate a random state string to prevent CSRF attacks
    state = str(uuid.uuid4())
    session["state"] = state

    # The magic URL that asks for Admin Consent for your specific Client ID
    auth_url = (
        f"{AUTHORITY}/adminconsent"
        f"?client_id={CLIENT_ID}"
        f"&state={state}"
        f"&redirect_uri={REDIRECT_URI}"
    )

    print(f"Redirecting client to: {auth_url}")
    return redirect(auth_url)

@app.route("/callback")
def callback():
    """Microsoft redirects the user here after they click Accept or Cancel"""
    
    # Check if the user cancelled or if there's an error
    error = request.args.get("error")
    error_description = request.args.get("error_description")
    if error:
        return f"<h3>Authentication Failed</h3><p>{error}: {error_description}</p>"

    # Verify the state matches
    returned_state = request.args.get("state")
    if returned_state != session.get("state"):
        return "<h3>Error: State mismatch error.</h3>"

    # SUCCESS! Microsoft gives us the Admin's Tenant ID.
    tenant_id = request.args.get("tenant")
    admin_consent = request.args.get("admin_consent")

    if admin_consent == "True":
        print(f"\nSUCCESS! Client onboarded. Their Tenant ID is: {tenant_id}\n")
        
        # IN PRODUCTION: You would save this tenant_id to your database here, associated with the client's Sorterra account.
        
        html = f"""
        <h1 style="color: green;">Successfully Connected!</h1>
        <p>Your SharePoint is now linked to Sorterra.</p>
        <p><strong>Your Admin Tenant ID:</strong> {tenant_id}</p>
        <p><i>(In production, we save this ID to the database so our background Agent knows which SharePoint to connect to.)</i></p>
        """
        return html
    else:
        return "<h3>Consent was not granted.</h3>"

if __name__ == "__main__":
    # Run the local server on port 5000
    print("Starting Sorterra SaaS Onboarding App...")
    print("Go to http://localhost:5000")
    app.run(port=5000, debug=True)
