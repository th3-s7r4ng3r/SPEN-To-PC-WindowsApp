# SPEN To PC Windows App: Developer Guide

## Introduction

The SPEN To PC Windows App is a part of the application that allows users to control their computers remotely using the S Pen Air Actions from Samsung mobile devices. This developer guide provides insights into the code structure, key functionalities, and usage of the SPEN To PC Windows App.

**For the Complete Guide:** [SPEN To PC Wiki](https://github.com/th3-s7r4ng3r/SPEN-To-PC-WindowsApp/wiki)

## Code Overview

The SPEN To PC Windows App is developed in C# using the WPF framework. The main code file, `MainWindow.xaml.cs`, encompasses the application's logic and user interface.

### Key Components

- **Persistent Connection Handling:** The app manages a persistent TCP connection to facilitate communication between the mobile device and the computer.
- **Settings Management:** User preferences, such as single-click and double-click actions, are stored and retrieved from a `settings.json` file located in the user Roaming folder.
- **Key Event Simulation:** The app simulates keypress events based on received data, translating S Pen gestures into corresponding actions on the computer.
- **UI Update and Styling:** The user interface dynamically updates to reflect the connection status and currently executed actions. Styling changes indicate active or inactive states.
- **IP Address Display:** The app displays the computer's IP address for connection reference.
- **Navigation Panels:** Different panels, such as Home, Settings, and About, are available for users to navigate through the app.
- **Key Capture:** Users can customize single-click and double-click actions by capturing keypress events.
- **Check Updates:** At the event of launching the application, it checks the [backend server](https://github.com/th3-s7r4ng3r/SPEN-To-PC-Backend) for updates.
- **Advanced Installer:** For distributing the application, Advanced Installer for Visual Studio is used to build the setup files.

## Developer Guide

### Prerequisites

Ensure you have the following installed:
- Visual Studio or any compatible C# development environment.
- .NET 8.0 Framework.
- Advanced Installer for Visual Studio (Optional)

### Getting Started

 1. **Clone the Repository:** Clone the SPEN To PC repository from [GitHub](https://github.com/th3-s7r4ng3r/SPEN-To-PC-WindowsApp).
 2. **Open in Visual Studio:** Open the project in Visual Studio by opening the `SPEN_To_PC_WindowsApp.sln` solution file.
 3. **Build and Run:** Build and run the project to test the application locally.


### Contribution Guidelines
If you wish to contribute to the project, follow these guidelines:

 1. Fork the repository.
 2. Create a new branch for your feature or bug fix.
 3. Make changes and ensure code quality.
 4. Submit a pull request.

### Contact and Support
For any inquiries or support, contact the developer:
**Email:** [th3.s7r4ng3r@gmail.com](mailto:th3.s7r4ng3r@gmail.com)

### Acknowledgments
The SPEN To PC Windows App is an open-source project. Contributions, feedback, and support are greatly appreciated.
