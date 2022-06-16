# ActivityPresenter - Frequently Asked Questions

Last updated: 2022-06-10

## Archives

**A1. How do I open and close an archive?**

  At the main page, press "OPEN" and select an archive file (.aaz) in the file-
  browser. The archive appears as a green box at the top right corner named as 
  the archive top level folder name.
  The content of the folder can be expanded by pressing the folder.
  Multiple archives can be opened at the same time by pression "OPEN".
  An archive can be closed by right-clicking the folder name and selecting 
  "CLOSE".

**A2. How do I save an archive?**

  Data is saved in archives as a single file named aaz. Select "SAVE PROJECT" 
  in the main menu to save data. The current folders and data will be shown in 
  the left window, while the structure of the new archive is shown in the right 
  window. To define a new archive you must start by adding a new top level 
  folder by dragging from "Add Folder" to the right window. 
  You may now add new folders or drag data from the left window to the new 
  archive. There are three types of folders: standard folders, data folders 
  and video folders. Standard folders may contain all type of folders, while 
  data folders must contain data-files and video folders must contain video
  files. An archive cannot be saved if there are any empty data or video
  folders. To rename a folder right click the folder-name. 
  When the folder structure is complete press "Save" and specify the location and name for the archive. 
  Note: Saving an archive may take some time, observe the progress bar to check 
  when saving is completed. The ActivityPresenter application should not be 
  terminated before saving is complete.  
  Note also that the saved archive is not automatically opened in 
  ActivityPresenter. To work with the new archive you must manually open the 
  new archive.

**A3. Is there a quick way to save an archive?**
  To quickly save the project select "Save Project", "Add all" and "Save".

**A4. How is data stored in an archive?**

  Data is stored in an uncompressed zip file named aaz (AutoActiveZip). Sensor 
  data is stored in parquet files, while videos are stored in their original 
  format, as they are already compressed. For further information, please see
  the "Data Handling" section in the paper.md file in the repository:

  https://github.com/SINTEF/AutoActive-ActivityPresenter/blob/develop/Paper/paper.md

**A5. Can I modify an archive?**

  To modify an archive you must create a new archive and copy and modify data 
  in the new archive. When a archive is saved, it becomes immutable and is 
  assigned a unique identifier. This allow sessions to be based on previous 
  sessions, and enables traceability and reproducibility as analysed information 
  is referenced to the session where the data for that analysis was stored.

**A6. Can I open multiple archives?**

  Yes, multiple archives can be opened and combined.


## Import

**I1. How do I import data in ActivityPresenter?**

  Data can be imported by selecting "IMPORT FILE" and selecting time-series data 
  or video files. ActivityPresenter will automatically detect the format of 
  supported data types. The time resolution must be in milli- or micro-seconds and is specified in a checkbox during import.

**I2. What data formats are supported by ActivityPresenter?**

ActivityPresenter supports the following formats:
* Comma separated values (csv)
* Excel (xlsx)
* GaitUp movement data (bin and Excel results)
* Garmin (tcx)
* Catapult (csv)
* json (annotations)
* MQTT (Message Queuing Telemetry Transport) [incomplete, framework only]

Data import is plug-in based, hence new formats can be added without deep knowledge of the ActivityPresenter application.

**I3. What video formats are supported by ActivityPresenter?**

  ActivityPresenter support video formatted as mov, avi, mkv, mp4, mts.

**I4. How are csv files imported and how is the timestamp converted?**

  For csv files ActivityPresenter will search for a column named "time" or 
  "epoch" and use this as the time basis. Time may be specified in milli- or 
  micro-seconds, and if specified as epoch it will be converted to date and 
  time. The import function will skip a header in the csv file before importing
  data. 
  For further details see the source file GenericCsvParser.cs


## Main page

**M1. How do I save and recall views?**

  You may Save and Load views by right clicking the top-level folder for 
  archives at the right-hand side at the "Main Page". 

**M2. Can I load a view from another archive?**

  Yes, view can be used interchangeable. However, if the specified data is not 
  present in the current dataset a message will be printed and that part of the 
  view will be skipped. Note that the views are loaded based on the order of the
  data in an archive so make sure to keep archives that uses views order in the
  same way.

**M3. What keyboard shortcuts can be used?**

  The following keyboard shortcuts are available:

     Spacebar             - Play and Stop

     Left                 - Step 1/30s back

     Right                - Step 1/30s forward

     Shift + Left         - Step 1s back

     Shift + Right        - Step 1s forward

     Ctrl + Left          - Step 10s back

     Ctrl + Right         - Step 10s forward

     Ctrl + Shift + Left  - Step 60s back

     Ctrl + Shift + Right - Step 60s forward

     0                    - remove annotation 

     1 to 9               - add annotation 1 to 9

     Shift + 1 to 9 and 0 - add annotation 10 to 19  

     Ctrl + 1 to 9 and 0  - add annotation 20 to 29  


**M4. When right clicking a dataset one may select Timeline, what does it mean?**

  A single data line may be shown as a complete timeline at the bottom of the
  main page, above the standard indication of the timeline for the various 
  data.

**M6. What is the meaning of the numbers at the top right corner of video view?**

  The top number shows the time difference between the current video view and
  the data view for the current frame. The bottom number shows the current
  playback delay of the video. As the typically takes a different amount of time
  through the system than the plotted data, this offset is applied. If this gets
  large, og changes rapidly, the computer might not be powerful enough to play
  the current view.

**M7. Can I change the view for the data-lines?**

  Yes, by pressing the three dots "..." at the dataline you may:
* remove the window 
* remove a line
* select - place the same line
* autoscale common - scale all lines in window to a common base
* autoscale independent - scale all lines in this view independently
* freeze scaling for line
* force y scale to whole numbers
* scatter plot
* column plot

     
**M8. What does the time shown at the right and left bottom side mean?**

  The time at the left show the time at the current position while the time
  at the left shows the time at the end of the dataset.


## Annotations

**AN1. What is the purpose of an annotation?**

  Annotations are used to mark specific part of video and data and is useful as
  input for machine learning and data analysis.

**AN2. How do I add an annotation?**

  An annotation is added at the current position in the main view by pressing 
  one of the keys 1 to 9, shift together with 1 to 9 and 0, or CTRL together 
  with 1 to 9 and 0 giving annotations from 1 to 29. 
  Pressing 0 will delete an annotation. 

**AN3. How do I view annotations?**

  Annotations appears at the bottom of the first data-line. A new data-line is
  created as AnnotationProvider and may be viewed as a separate window.

**AN4. Can I name and describe an annotation?**

  Annotations may be named by pressing "Settings" followed by "Annotations". 
  Each annotation is mapped from the ID and can be given a Name, a Tag and a 
  Comment. If a Tag is given this is shown for the annotation at the Main Page.

**AN5. How many different annotations can be added?**

  You may define 29 individual annotations.
  
**AN6. Can annotations be accessed from the Matlab or Python toolbox?**

  Yes, annotations may be accessed from the toolboxes. Please see examples in
  the toolboxes. 


## Settings

**SE1. Can I change the scaling of the data-lines?**

  Yes, by changing the Window Length in the Settings menu you may change the
  scaling of data-lines.

  The Window Length is saved when selecting Save View and recalled with Load View.

**SE2. Can I change the playback speed for video and data-lines?**

  The playback speed can be set to 1x, 2x, 5x, 0.1x, 0.25x and 0.5x in the 
  settings menu.

  The playback speed is saved when selecting Save View and recalled with Load View.


## Synchronization

**S1. What is the purpose of the synchronize page?**

  Data from different sources are often not at the same time reference. To be  
  able to analyse video and timeseries data must be synchronized. The 
  synchronization page let the user synchronize data from different sources.
  
**S2. How do I synchronize data?**

  First select the dataset that should be master with respect to time, you may 
  view multiple data-lines or a video in the master view. When selecting data  
  from another folder this will appear as slave as it is not at same time 
  reference. 
  First find a reference point in the master data (e.g. from calibration 
  routine in data collection) by dragging in the timeline and fine adjust using 
  forward and back buttons below the Master window. The select "SET SYNC POINT" 
  for Master window. Then find the corresponding point in the slave data and  
  select "SET SYNC POINT" for the slave window. Then save the synchronization by 
  selecting "Save Sync". The relative time difference is stored in the 
  time data for the slave. Play through the dataset to verify that 
  synchronization is correct.

  See video for synchronization here:

  https://user-images.githubusercontent.com/2269482/115603893-ff07b000-a2e0-11eb-8327-e0b5244880c8.mp4
  
**S3. What does the "Mark Features" do?**

  The "Mark Features" in the Synchronization window will set a marker in the 
  timeline. This can be useful for marking the synchronization point.

**S4. What is AutoSync" and how does it work?**

  Timeseries data from two sources can be automatically synchronized using the 
  AutoSync feature. This feature is based on autocorrelation between time series 
  signals. Open the master and slave timeline data and press AutoSync. Select a 
  correlation point and press "Save Sync" to use the synchronization point.
  See video for AutoSync here:

  https://user-images.githubusercontent.com/2269482/115604090-4130f180-a2e1-11eb-9505-2d79537ac827.mp4

**S5. I have a set with synchronized GaitUp IMU files I would like to synchronize with a video. How can I avoid synchronizing every GaitUp file with the video?**

  GaitUp IMU data is stored in a set of binary files. These are synchronized
  towards each other from the GaitUp software. Hence when synchronizing towards 
  a video in ActivityPresenter it is recommended to start with a GaitUp datafile 
  and synchronize the video towards the GaitUp data. This means put the gaitup
  data in the left master synchronization window and the video in the slave 
  window.
You may synchronize starting with a video, however you must the apply the same offset to all GaitUp data files. 


## Head-to-head

**H1. What is the purpose of the head2head view?**

  The Head to Head view allows two parts of a dataset to be viewed 
  simultaneously. This can be used to compare techniques at different times 
  references or for different users.


## Matlab toolbox

**MT1. What is the purpose of the Matlab toolbox?**

  The Matlab toolbox is made to allow researchers to easily process data and to
  automate import of data.
  
**MT2. Where can I find further information about the Matlab toolbox?**

The Matlab toolbox is documented in the Matlab Getting Started live script as 
well as at the GitHub page: 

https://github.com/SINTEF/AutoActive-Matlab-toolbox 

**MT3. Where can I find example code for the Matlab toolbox?**

Sample code for the Matlab toolbox is available here:

https://github.com/SINTEF/AutoActive-Matlab-toolbox/tree/master/MatlabToolbox/examples


## Python toolbox

**PT1. What is the purpose of the Python toolbox?**

  The Python toolbox is made to allow researchers to easily process data and to
  automate import of data.
  
**PT2. Where can I find further information about the Python toolbox?**

The Python toolbox is documented at the GitHub page: 

https://github.com/SINTEF/AutoActive-Python-toolbox

**PT3. Where can I find example code for the Python toolbox?**

Sample code for the Python toolbox is available here:

https://github.com/SINTEF/AutoActive-Python-toolbox/tree/master/examples 


## Installation

**IN1. How do I install ActivityPresenter?**

ActivityPresenter is available as a Microsoft Windows App from Microsoft Store:

https://www.microsoft.com/en-us/p/activity-presenter/9n01v94ljlx7

You may also build your own binary from the source code as described at the
GitHub page: 

https://github.com/SINTEF/AutoActive-ActivityPresenter
  
**IN2. Is ActivityPresenter available for other platforms than Microsoft Windows?**

  Currently only Microsoft Windows is supported. However the ActivityPresenter 
  software is implemented using Xamarin and Xamarin.Forms. A Xamarin app runs on 
  various platforms through bindings to a native runtime component. For Xamarin, 
  this runtime is a Common Language Runtime (CLR), which was created for 
  Windows, but is also supported on Linux, Android and iOS through the Mono 
  project.   Data between the various application parts is shared through a 
  common internal data bus providing a flexible architecture. Porting to 
  multiple platforms is a future goal.

**IN3. How do I install the AutoActive Matlab toolbox?**

A binary distribution of the Matlab toolbox can be downloaded as an "Asset" for the releases at the toolbox Github page:

https://github.com/SINTEF/AutoActive-Matlab-toolbox

Open the ".mltbx" file in Matlab to install the toolbox.
  
**IN4. How do I install the AutoActive Python toolbox?**

  To use the AutoActive Python package you need to clone this repository and 
  import it into your projects of interest.


## Examples

**E1. Where to look for Examples?**

The use of ActivityPresenter is shown in the videos available at:

https://github.com/SINTEF/AutoActive-ActivityPresenter

A cross-country skiing example using ActivityPresenter, the Matlab toolbox and annotations is available here:

[TBD - to be added] 

There are sample code available for both the Matlab and Python toolboxes, please see sections for the toolboxes.

## More documentation

**MD1. Where to look for further documentation?**

Please see the following page for further documentation:

https://github.com/SINTEF/AutoActive-ActivityPresenter/blob/develop/README.md

**MD2. Instruction videos for ActivityPresenter**

* [Import data](https://user-images.githubusercontent.com/2269482/115543050-3fdfd480-a2a1-11eb-8c5d-1150adb3e2b1.mp4)
* [Syncronize data](https://user-images.githubusercontent.com/2269482/115603893-ff07b000-a2e0-11eb-8327-e0b5244880c8.mp4)
* [Save data](https://user-images.githubusercontent.com/2269482/115671110-758cc800-a34a-11eb-86a7-d1c8d9439a22.mp4)
* [Visualize data](https://user-images.githubusercontent.com/2269482/115603974-1ba3e800-a2e1-11eb-9660-a314641c0cd6.mp4)
* [Matlab toolbox](https://user-images.githubusercontent.com/2269482/115671205-8dfce280-a34a-11eb-8892-1031a8101da4.mp4)
* [Multiple aaz archives](https://user-images.githubusercontent.com/2269482/115604046-324a3f00-a2e1-11eb-8253-8d73e52fba69.mp4)
* [Automatic synchronization of data](https://user-images.githubusercontent.com/2269482/115604090-4130f180-a2e1-11eb-9505-2d79537ac827.mp4)
* [Head to head](https://user-images.githubusercontent.com/2269482/115604146-5443c180-a2e1-11eb-802a-2e11029f6781.mp4)
* [Save and load views](https://user-images.githubusercontent.com/2269482/115604181-5dcd2980-a2e1-11eb-8cb7-bb1b40573ea9.mp4)


**MD3. Publications**

A publication for the AutoActive Reserach Environment is available in the Journal of Open Source Software (JOSS):
https://joss.theoj.org/papers/10.21105/joss.04061

**MD4. Release notes/change log**
Please see the following file for release notes/change log:

https://github.com/SINTEF/AutoActive-ActivityPresenter/blob/develop/CHANGELOG_AP.txt

