## How to develop on the package within a Unity project
To properly develop on the package, it needs to be integrated into a Unity project using an approach allowing for package modification. The most common approach is to import the package into a Unity project using the Package Manager import from disk.

**Step by step**
1. Clone the git repo of the package to a location on disk.
2. Open the Unity project in which you want to develop. 
3. Go to Package Manager and add the package by selecting "+" -> "Add package from disk...".
4. Browse to the location of the packages's git repo and select package.json.

Changes on the package performed within the Unity project need to be commited within the cloned repo of the package.

**For constantly developing on the essentials within a larger project, we recommend to use [Git Submodule](https://git-scm.com/book/de/v2/Git-Tools-Submodule).**

