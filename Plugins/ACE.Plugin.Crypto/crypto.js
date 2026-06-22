{
	 "CryptoConfiguration": {
		
        /*
		 *  For hosting the web services.
		 *  Specify the full path to a PKCS #12 file containing a private key with its X.509 certificate.
		 *  Leave this null to automatically generate a new self-signed one for this server upon first server start with the web services enabled.
		 *  Automatic creation will save and use the file at:
		 *  Linux: <ACE>/certificates/web.pfx
		 *  Windows: <ACE>\certificates\web.pfx
		 */
		"FilePathKeyAndCertBundleWeb": null,
		// If supplying an encrypted PKCS #12 file for Web and want to manually enter the password via the console during server startup.
		"PasswordStartupPromptKeyAndCertBundleWeb": false,
		// If supplying an encrypted PKCS #12 file for Web and not using password startup prompt mode.  Disables password startup prompt mode for Web if supplied.  Default empty/no password, example: "mypassword"
		"PasswordKeyAndCertBundleWeb": "",
		/*
		 *  For signing character data.
		 *  Applicable when using character transfer features.
		 *  Specify the full path to a PKCS #12 file containing a private key with its X.509 certificate.
		 *  Leave this null to automatically generate a new self-signed one for this server upon first server start with one or more character transfer features enabled.
		 *  Automatic creation will save and use the file at:
		 *  Linux: <ACE>/certificates/datasigner.pfx
		 *  Windows: <ACE>\certificates\datasigner.pfx
		 *  After creation keep the file private and it's a good idea to backup the file to a private location.
		 *  If an adversary obtains it they could use it maliciously against servers that trust its certificate/server thumbprint!
		 *  If this file is lost all servers that trust its certificate/server thumbprint will need to be reconfigured and all local pending migrations cancelled.
		 *  If the world name is changed then this file should be moved to the new location.
		 *  Note: <worldname> is a version of the configured world name of only the characters a-z, 0-9, no spaces, no specials, converted to lowercase.
		 */
		"FilePathKeyAndCertBundleDataSigner": null,
		// If supplying an encrypted PKCS #12 file for DataSigner and want to manually enter the password via the console during server startup.
		"PasswordStartupPromptKeyAndCertBundleDataSigner": false,
		// If supplying an encrypted PKCS #12 file for DataSigner and not using password startup prompt mode.  Disables password startup prompt mode for DataSigner if supplied.  Default empty/no password, example: "mypassword"
		"PasswordKeyAndCertBundleDataSigner": ""

	}
}