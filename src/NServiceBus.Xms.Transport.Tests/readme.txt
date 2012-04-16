Before running these test you need to install:
IBM WebSphere MQ (7.0.0.1)
IBM Message Service Client for .NET (Version 2.0.0.6)

Also 2 queues should be created and app.config changed accordingly

<appSettings>
    <!--
    Format: queue@queueManager/host/port/channel    
    -->
    <add key="target" value="MQ_TRANSPORT_INPUT@QM_ALPHA_DEV_01/localhost/1414/SYSTEM.DEF.SVRCONN"/>
    <add key="error" value="MQ_TRANSPORT_ERROR@QM_ALPHA_DEV_01/localhost/1414/SYSTEM.DEF.SVRCONN"/>
</appSettings>