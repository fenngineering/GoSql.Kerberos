# Introduction
As you are all very well aware Linux to Windows Authentication for access to SQL Server can be a slippery slope to doom. So I have decided to create an tutorial on how to successfully authenticate with a NT User from Linux to Windows (cross platform) to access a SQL Server.

# The Lab (Prerequisites)
I have a Windows 10 Development Environment with the below installed:

* Visual Studio Code with extentions, [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp), [YAML](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) and [Json Prettify](https://marketplace.visualstudio.com/items?itemName=mohsen1.prettify-json)
* Oracle Virtual Box 6.1 running:-
   * **Windows Server Running Active Directory**, for this Lab I will be using Windows Server 2019 Core evaluation available [here](https://www.microsoft.com/en-us/evalcenter/evaluate-windows-server-2019).  The server has been promoted to DC with DNS setup and a user created for SQL Server access, all of which are out of scope for this Lab. The domain created is GOSQL.CO.UK.
   * **SQL Server** joined to the above Active Directory domain, for this Lab I will be using Windows Server 2019 Core running SQL Server 2019 evaluation available [here](https://www.microsoft.com/en-us/evalcenter/evaluate-sql-server-2019). A login has been created with the above windows user and granted access to our database. Also SPNs need to be set for SQL Server help guides available [here](https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/register-a-service-principal-name-for-kerberos-connections?view=sql-server-ver15 ). 
   * **MicrosK8s** server running on Ubuntu 20.04, with DNS & Dashboard modules added, the setup is out of scope for this Lab. 

I have created a "Host-Only" network in Virtual Box which has DHCP configured as below:

[image]VirtualBoxHostOnlyNIC.PNG[/image] [image]VirtualBoxHostOnlyDHCP.PNG[/image]

Each server has been allocated 2 vCPUs, with 6GB vRAM AND 20GB vHDD.

Each server has 2 Network adapters, one configured for Host-Only, the other is configured for NAT.

Port Forwarding has been configured as below:-

## MicrosK8s
[image]K8sPortForwarding.PNG[/image]

## SQL Server
[image]SQLPortForwarding.PNG[/image]

Here we are opening ports for RDP, so that if we should need to we can remote desktop on the Guest from the Host. We are also opening 1433 so that we can connect to SQL Server from the Guest to SSMS on the Host.

[image]ConnectToSQLFromHost.PNG[/image]

# Server Configurations
The servers will be configured as below:

## Domain Controller
* Server Name: domain
* Forest root domain: gosql.co.uk
* IP: 192.168.1.2 - Static

## SQL Server
* Server Name: sql
* Joined to domain: gosql.co.uk
* IP: 192.168.1.3 - Static

## K8s 
* Server Name: k8s
* Not joined to a domain.
* IP: DHCP

# Scenario
I have put together a dotnet core console application with a connection to the SQL Server which has Integrated Security enabled, for this Lab I will be using Microsoft DotNet Core 3.1. The code base is available here. ToDo create codebase.

The connection string is created as below:
`"Server=sql.gosql.co.uk;Database=gosql;Integrated Security=True;";`

As you can see the Fully Qualified Domain Name (FQND) has been used, this is a requirement for Kerberos authentication and must be entered, "sql" alone as the Server name is not enough for the token to be authenticated.

The connection string has been hardcoded for the lab, however it is best practice to set this in the appSettings.json configuration file (With seperate Development and Production counter parts see [here](https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration) for more information)

I will be deploying the application twice using Microk8s, one without Kerberos configurations, and again with Kerberos configurations. I will follow with a detailed conclusion after each deployment.

## Deployment #1 (Without Kerberos Configurations)
For this deployment, we are going to create a docker image and import it into Micok8s, once imported we can then create a K8s pod and run the application inside the container.

### Dockerfile-NoKerb

[Dockerfile-NoKerb](https://)

### Docker build, save, K8s import for DB Application
In Windows Development Environment

`cd c:\gosql\GoSql.Kerberos\DBApp`

`docker build Dockerfile-NoKerb . -t gosql-dbapp:latest`

`docker save gosql-dbapp -o gosql-dbapp.tar `

In MicroK8s

`cd /mnt/gosql/GoSql.Kerberos/DBApp`

`sudo microk8s ctr image import gosql-dbapp.tar` 

I have mapped a shared folder from the Host running Virtual Box (c:\gosql) to the K8s server so that the saved docker image can be imported into Microk8s. When you have succesfully added the image to K8s you should be able to use it in the YAML spec.

`unpacking docker.io/library/gosql-dbapp:latest 

(sha256:71fdaaaf83eb99deb12d3abf84f7b59a789b70abc8c1400ade9e0b9839c3c1cc)...done`

### MicroK8s Pod YAML spec

[no-kerb-dbapp.k8s.yml](https://)

### Conclusion
As you can see the deployment will fail as below screen shot taken from within the K8s dashboard.

[image]NoKerbErrMsg.PNG[/image]
 
This is due to the lack of Kerberos configurations required to run the DB Application.

## Deployment #2 (With Kerberos Configurations)
We are going to take the approach of creating a keytab from a Windows account, then have another Pod to act as a "Side-Car" to renew the token on a defined period, we are using every 10 seconds for this Lab, you may want to change this to every minute. The renewed token will then be used within a shared memory volume so that the DB Application can authenticate against it in the connection to the SQL Server. 

I have modified the K8s spec to include the required Kerberos configuration.

### Dockerfile for DB Application

[DBApp/Dockerfile](https://)

As you can see I have added lines 12-17. This installs the Kerberos client tools into the image so that we can utilise them when renewing a token from a keytab file, I'll discuss more about this later.

### Dockerfile for Side-Car Pod
This is a new image for the container, as mentioned ealier we must renew our token, to do that you use an application `kinit` this is ran in the `rekinit.sh` shell file which is the entry point to the image.

[SideCar/Dockerfile](https://)

Lines 2-3 installation of the Kerberos client tools.
Lines 4-5 add resources, the kinit script and the default krb5 configuration.
Line 6 configures the exported volumes:
 * /krb5 - default keytab location.
 * /dev/shm - shared memory location used to write token cache.
 * /etc/krb5.conf.d - directory for additional kerberos configuration.
Line 7 runs the image using command sh /rekinit.sh.

### RunKinit.sh

[SideCar/RunKinit.sh](https://)

This script is run when the image starts. And will run infinately until either a fault or the system is rebooted then it would restart. The Shell scipt will:
* Report to stdout the time the kinit was being run.
* Run kinit with passed options, note APPEND_OPTIONS allows for additional parameters to be configured. The verbose option is always set. 
* Report valid tokens
* Finally sleeps for the defined period, then repeat.

### Docker build, save, K8s import for Side-Car 
In Windows Development Environment

`cd c:\gosql\GoSql.Kinit`

`docker build . -t gosql-kinit:latest`

`docker save gosql-kinit -o gosql-kinit.tar `

In MicroK8s

`cd /mnt/gosql/GoSql.Kinit`

`sudo microk8s ctr image import gosql-kinit.tar` 

### Docker build, save, K8s import for DB Application

In Windows Development Environment

`cd c:\gosql\GoSql.Kerberos\DBApp`

`docker build . -t gosql-dbapp:latest`

`docker save gosql-dbapp -o gosql-dbapp.tar `

In MicroK8s

`cd /mnt/gosql/GoSql.Kerberos/DBApp`

`sudo microk8s ctr image import gosql-dbapp.tar` 

## MicroK8s YAML spec
Before this will work you must create a keytab file which is used to authenticate kerberos tokens, more information about the keytab file visit this [page](https://web.mit.edu/kerberos/krb5-devel/doc/basic/keytab_def.html).

SSH on to the Microk8s server and create the keytab file:

`cd ~/`

`ktutil `

`ktutil: addindent -password -p dbuser@GOSQL.CO.UK -k -e aes128-cts-hmac-sha1-96`

**Please enter the password for the user.**

`ktutil: wkt dbuser.keytab`

This keytab **must be added** to Microk8s as a secret so that the container can read and renew the tokens.

Use Kinit to validate the keytab file:

`sudo kinit -V -kt /krb5/dbuser.keytab dbuser`

You should see the below:

`Using default cache: /tmp/krb5cc_0`

`Using principal: dbuser@GOSQL.CO.UK`

`Using keytab: /krb5/dbuser.keytab`

`Authenticated to Kerberos v5`

Once successfully authenticated add the keytab to microk8s

`microkk8s.kubectl create secret generic keytab --from-file=./dbuser.keytab`

You should see the below when successful:

`secret/keytab created`

Login to the Microk8s dashboard and add the below spec.

[YAMLs/kerb-dbapp.k8s.yml](https://)

There are quite a lot of differences in this version of the K8s YAML spec. What we are achiving here is:
1. Creating 2 Pods in one container, the Side-Car which will run the Kinit command to renew the tokens, the other is the DB Application which will access the shared memory volume which contains the authenticated tokens.
2. Creating the krb5.conf ConfigMap which is later mounted in both Pods, the krb5.conf is required for Kerberos authentication.
3. Creating volumes for ccache which is a file in memory used to store the authenticated tokens. See [here](https://kubernetes.io/docs/concepts/storage/ephemeral-volumes/) for more information on Memory volumns. 
4. We are mounting the krb5.conf from the above [ConfigMap](https://kubernetes.io/docs/tasks/configure-pod-container/configure-pod-configmap/) and the keytab from a [Secret](https://kubernetes.io/docs/concepts/configuration/secret/) file.
5. In both Pods we are mounting all the above volumes.
6. We are adding both the Domain Controller and the SQL Server to the /etc/hosts in each Pod so that the Pods can communicate with each other using the `hostAliases` section of the spec.

If you look into the Logs section for the Side-Car and DB Application you will notice that the Console Application is now fully authenicated from Linux to Windows using Kerberos:

[image]Pods.PNG[/image]

Hope you enjoyed reading and I look forward to your feedback.