# HikVision-Camera-Monitor
This application monitors events from a HikVision camera. It allows subscribing to certain events, filter events by state (Active states only for example), storing the information in a SQL database and sending email alerts.

How to use:

First, edit the application config file. Provide a connection string to your SQL database. Provide all the events that you would like to monitor along with the conditiona and actions that should be taken on each event.

Future plans:
Integrate with OpenHab home automation. Most hikvision cameras allow defining boundary lines and fire events when those lines are crossed. Home automation integratino would allow to react on such events (for example turning on the area linghts).
