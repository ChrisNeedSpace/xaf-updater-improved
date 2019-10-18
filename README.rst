===========================================================================
XAF Deployment - Updater tool with configurable removing of old dlls - v1.0
===========================================================================
-----------------------------
Compatible with XAF versions:
-----------------------------
- 17.2.11
- 18.1.6
- 18.2.8
- 19.1.6

-----------
Description
-----------
This is a feature for **DevExpress eXpressAppFramework (XAF)** which provides an enhanced Updater deployment tool. It includes configurable parameters that anable removing old version of XAF dlls. It is based on DevExpress.ExpressApp.Updater.exe source code from DevExpress.

**Functionality**

It is possible to set the following parameters in the config:

- **<add key="DeleteExistingFiles" value="True" />** - specify if existing files should be deleted in the destination machine. Defaults to false.
- **<add key="IgnoredFolders" value="Logs" />** - specify folders which will be ignored in the update process. The values are , or ; separated.
- **<add key="IgnoredFilePatterns" value="LogonParameters;*.log;DevExpress.ExpressApp.Updater.pdb" />** - specify file patterns that indicate which files will be ignored in the update process. The values are , or ; separated.

**IMPORTANT NOTE:**

On newer XAF versions this tool needs to be manually merged with the original DevExpress.ExpressApp.Updater.exe source code.

**Example Of Use**

Use it the same way as the XAF Updater tool. The usage is described in this article:
https://documentation.devexpress.com/eXpressAppFramework/113239/Deployment/Deployment-Tutorial/Application-Update

**Screenshots**

Solves the issue of not deleting old XAF dlls:

.. image:: https://raw.github.com/KrzysztofKielce/xaf-updater-improved/master/Screenshot1.png

with the following config settings available:

.. image:: https://raw.github.com/KrzysztofKielce/xaf-updater-improved/master/Screenshot2.png

------------
Installation
------------
#. Install XAF_.
#. Get the source code for this plugin from github_, either using `Source Tree`_, or downloading directly:

   - To download using Source Tree, install and open Source Tree, add new tab, click Clone, then enter the repository url:

     ``https://github.com/KrzysztofKielce/xaf-updater-improved.git``
   - To download directly, go to the `project page`_ and click **Download**

#. Open XAF Win project (or create one) and follow the deployment instructions from https://documentation.devexpress.com/eXpressAppFramework/113239/Deployment/Deployment-Tutorial/Application-Update


.. _XAF: http://go.devexpress.com/DevExpressDownload_UniversalTrial.aspx
.. _Source Tree: https://www.sourcetreeapp.com/
.. _github:
.. _project page: https://github.com/KrzysztofKielce/xaf-updater-improved


----------
Disclaimer
----------
This is **beta** code.  It is probably okay for production environments, but may not work exactly as expected.  Refunds will not be given.  If it breaks, you need to keep both parts.

-------
License
-------
All code herein is under the Do What The Fuck You Want To Public License (WTFPL_).

.. _WTFPL: http://www.wtfpl.net/

---------
About XAF
---------
The eXpressApp Framework (XAF) is a modern and flexible application framework that allows you to create powerful line-of-business applications that simultaneously target Windows and the Web. XAF's scaffolding of the database and UI allows you to concentrate on business rules without the many distractions and tedious tasks normally associated with Windows and Web development. XAF's modular design facilitates a plug and play approach to common business requirements such as security and reporting.

XAF's advantages when compared with a more traditional approach to app development are profound. See for yourself and learn why XAF can radically increase productivity and help you bring solutions to market faster than you've ever thought possible. 

For more information, visit:

http://www.devexpress.com/Products/NET/Application_Framework/
