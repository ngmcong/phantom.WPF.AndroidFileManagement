import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:filemanagement/dataentities.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:path_provider/path_provider.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:http/http.dart' as http;
import 'package:signalr_netcore/hub_connection.dart';
import 'package:signalr_netcore/hub_connection_builder.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  // This widget is the root of your application.
  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Flutter Demo',
      theme: ThemeData(
        // This is the theme of your application.
        //
        // TRY THIS: Try running your application with "flutter run". You'll see
        // the application has a purple toolbar. Then, without quitting the app,
        // try changing the seedColor in the colorScheme below to Colors.green
        // and then invoke "hot reload" (save your changes or press the "hot
        // reload" button in a Flutter-supported IDE, or press "r" if you used
        // the command line to start the app).
        //
        // Notice that the counter didn't reset back to zero; the application
        // state is not lost during the reload. To reset the state, use hot
        // restart instead.
        //
        // This works for code too, not just values: Most code changes can be
        // tested with just a hot reload.
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.deepPurple),
      ),
      home: const MyHomePage(title: 'Flutter Demo Home Page'),
    );
  }
}

class MyHomePage extends StatefulWidget {
  const MyHomePage({super.key, required this.title});

  // This widget is the home page of your application. It is stateful, meaning
  // that it has a State object (defined below) that contains fields that affect
  // how it looks.

  // This class is the configuration for the state. It holds the values (in this
  // case the title) provided by the parent (in this case the App widget) and
  // used by the build method of the State. Fields in a Widget subclass are
  // always marked "final".

  final String title;

  @override
  State<MyHomePage> createState() => _MyHomePageState();
}

class _MyHomePageState extends State<MyHomePage> {
  ListViewAPIModel? _listViewAPIModel;

  Future<void> requestStoragePermission() async {
    var status = await Permission.storage.request();
    if (status.isGranted) {
      if (kDebugMode) {
        print("Storage permission granted.");
      }
      // Proceed with accessing storage
    } else if (status.isDenied) {
      if (kDebugMode) {
        print("Storage permission denied by user.");
      }
      // Optionally show a rationale for why you need the permission
    } else if (status.isPermanentlyDenied) {
      if (kDebugMode) {
        print(
          "Storage permission permanently denied. Open app settings to grant.",
        );
      }
      openAppSettings();
    }
  }

  Future<bool> requestManageExternalStoragePermission() async {
    var status = await Permission.manageExternalStorage.request();
    if (status.isGranted) {
      if (kDebugMode) {
        print("MANAGE_EXTERNAL_STORAGE permission granted");
      }
      return true;
    } else if (status.isDenied) {
      if (kDebugMode) {
        print("MANAGE_EXTERNAL_STORAGE permission denied");
      }
      // Handle the denied state (e.g., show a message)
      return false;
    } else if (status.isPermanentlyDenied) {
      if (kDebugMode) {
        print("MANAGE_EXTERNAL_STORAGE permission permanently denied");
      }
      // Guide the user to open app settings
      openAppSettings();
      return false;
    }
    return false;
  }

  Future<void> checkStoragePermission() async {
    var status = await Permission.storage.status;
    if (status.isGranted) {
      if (kDebugMode) {
        print("Storage permission is granted.");
      }
      // Proceed with accessing storage
    } else if (status.isDenied) {
      if (kDebugMode) {
        print("Storage permission is denied. Requesting permission...");
      }
      await requestStoragePermission();
    } else if (status.isPermanentlyDenied) {
      if (kDebugMode) {
        print("Storage permission is permanently denied. Open app settings.");
      }
      openAppSettings(); // Opens the app's settings page
    } else if (status.isRestricted) {
      if (kDebugMode) {
        print("Storage permission is restricted on this device.");
      }
    }
  }

  Future<void> _postFiles() async {
    final url = Uri.parse('http://192.168.2.105:5001/api/listview');
    final response = await http.post(
      url,
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(
        _listViewAPIModel,
      ), // Convert the list of products to JSON
    );
    if (response.statusCode == 200) {
      // var responseBody = jsonDecode(response.body);
      // if (kDebugMode && responseBody.code == 0) {
      //   if (kDebugMode) {
      //     print('Table products saved successfully');
      //   }
      // }
      // if (responseBody.code != 0) throw Exception(responseBody.message);
    } else {
      throw Exception('Failed to save table products');
    }
  }

  Future<void> _loadFiles({
    String? filePath = '/storage/emulated/0/Download/',
  }) async {
    // Check if the permission is granted before accessing the directory
    await requestManageExternalStoragePermission();
    var directory =
        Platform.isAndroid
            ? Directory(filePath ?? '/storage/emulated/0/Download/')
            : await getDownloadsDirectory();
    List<FileSystemEntity> entities = directory?.listSync() ?? [];
    List<ListViewModel> filePaths = [];
    for (final entity in entities) {
      // if (entity is File) {
      //   filePaths.add(ListViewModel(path: entity.path));
      // }
      filePaths.add(
        ListViewModel(
          path: entity.path,
          type: (entity is File ? "File" : "Folder"),
          name: entity.path.split('/').last,
          size: (entity is File ? entity.statSync().size.toString() : null),
          dateModified: entity.statSync().modified.toString(),
        ),
      );
    }
    // Update the state with the loaded file paths
    setState(() {
      _listViewAPIModel ??= ListViewAPIModel(path: filePath, files: filePaths);
      _listViewAPIModel!.path = filePath;
      _listViewAPIModel!.files = filePaths;
    });
    _postFiles();
  }

  String serverUrl = "http://192.168.2.105:5001/myhub";

  HubConnection? _hubConnection;

  Future<void> deleteDirectory(String directoryPath) async {
    Directory directoryToDelete = Directory(directoryPath);
    try {
      if (await directoryToDelete.exists()) {
        await directoryToDelete.delete();
        if (kDebugMode) {
          print(
            'Directory deleted successfully: $directoryPath (in Quảng Ngãi on ${DateTime.now()})',
          );
        }
      } else {
        if (kDebugMode) {
          print('Directory does not exist: $directoryPath');
        }
      }
    } catch (e) {
      if (kDebugMode) {
        print('Error deleting directory $directoryPath: $e');
      }
      // Handle the error appropriately
    }
  }

  Future<void> deleteFile(String filePath) async {
    File fileToDelete = File(filePath);
    try {
      if (await fileToDelete.exists()) {
        await fileToDelete.delete();
        if (kDebugMode) {
          print(
            'File deleted successfully: $filePath (in Quảng Ngãi on ${DateTime.now()})',
          );
        }
      } else {
        if (kDebugMode) {
          print('File does not exist: $filePath');
        }
      }
    } catch (e) {
      if (kDebugMode) {
        print('Error deleting file $filePath: $e');
      }
      // Handle the error appropriately (e.g., permission issues)
    }
  }

  Future<void> _initHub() async {
    _hubConnection =
        HubConnectionBuilder()
            .withUrl(serverUrl)
            .withAutomaticReconnect() // Optional: Enable automatic reconnect
            // You can configure other options here, like hub protocols or logging
            .build();
    _hubConnection!.on('ReceiveMessage', (List<Object?>? arguments) async {
      String user = arguments?[0] as String? ?? '';
      String message = arguments?[1] as String? ?? '';
      // Handle the received user and message in your Flutter UI
      if (kDebugMode) {
        print('$user: $message (Received from .NET Host)');
      }
      if (user == "DELETE") {
        var filePath = message
            .split('/')
            .sublist(0, message.split('/').length - 1)
            .join('/');
        if (kDebugMode) {
          print('delete $message and reload at $filePath');
        }
        try {
          await deleteDirectory(message);
          await deleteFile(message);
        } catch (e) {
          if (kDebugMode) {
            print('Error deleting file or directory: $e');
          }
        }
        _loadFiles(
          filePath: filePath,
        ); // Reload files after receiving a message
      } else {
        _loadFiles(filePath: message); // Reload files after receiving a message
      }
      // Update your Flutter UI using setState or a state management solution
    });

    if (_hubConnection!.state != HubConnectionState.Connected) {
      try {
        await _hubConnection!.start();
        if (kDebugMode) {
          print('Connected to SignalR .NET Host!');
        }
        // Perform any actions needed after successful connection
      } catch (e) {
        if (kDebugMode) {
          print('Error connecting to SignalR .NET Host: $e');
        }
        // Handle connection errors (e.g., retry mechanism)
      }
    }

    // _hubConnection.on('UserConnected', (List<Object?>? arguments) {
    //   final String userId = arguments?[0] as String? ?? '';
    //   print('$userId connected to the chat.');
    //   // Update UI accordingly
    // });
  }

  Future<void> disconnectFromSignalR() async {
    if (_hubConnection != null &&
        _hubConnection!.state == HubConnectionState.Connected) {
      await _hubConnection!.stop();
      if (kDebugMode) {
        print('Disconnected from SignalR .NET Host.');
      }
    }
  }

  Future<void> uploadLargeFileHttp(
    File file,
    String apiUrl, {
    int chunkSize = 4 * 1024 * 1024,
  }) async {
    int totalSize = await file.length();
    int offset = 0;
    int partNumber = 1;
    String fileName = file.path.split('/').last;
    int totalParts = (totalSize / chunkSize).ceil();

    Stream<List<int>> fileStream = file.openRead();

    await for (final chunk in fileStream.transform(
      StreamTransformer.fromHandlers(
        handleData: (List<int> data, EventSink<List<int>> sink) {
          if (offset < totalSize) {
            int end = offset + chunkSize;
            if (end > totalSize) {
              end = totalSize;
            }
            sink.add(
              data.sublist(
                0,
                end - offset > data.length ? data.length : end - offset,
              ),
            );
            offset = end;
          } else {
            sink.close();
          }
        },
        handleDone: (EventSink<List<int>> sink) {
          sink.close();
        },
        handleError: (error, stackTrace, EventSink<List<int>> sink) {
          if (kDebugMode) {
            print('Error reading file stream: $error');
          }
          sink.addError(error, stackTrace);
        },
      ),
    )) {
      if (chunk.isNotEmpty) {
        var request = http.MultipartRequest('POST', Uri.parse(apiUrl));
        request.files.add(
          http.MultipartFile.fromBytes('fileChunk', chunk, filename: fileName),
        );
        request.fields['fileName'] = fileName;
        request.fields['totalSize'] = totalSize.toString();
        request.fields['offset'] =
            (offset - chunk.length).toString(); // Correct offset
        request.fields['partNumber'] = partNumber.toString();
        request.fields['totalParts'] = totalParts.toString();

        try {
          var streamedResponse = await request.send();
          var response = await http.Response.fromStream(streamedResponse);

          if (response.statusCode == 200) {
            if (kDebugMode) {
              print(
                'Chunk $partNumber uploaded successfully. Response: ${response.body}',
              );
            }
          } else {
            if (kDebugMode) {
              print(
                'Error uploading chunk $partNumber: ${response.statusCode} - ${response.body}',
              );
            }
            return;
          }
        } catch (e) {
          if (kDebugMode) {
            print('Exception during chunk $partNumber upload: $e');
          }
          return;
        }

        partNumber++;
      }
      if (offset >= totalSize) break;
    }

    if (kDebugMode) {
      print('File upload completed successfully using http!');
    }
  }

  @override
  void dispose() {
    disconnectFromSignalR().then((_) {
      if (kDebugMode) {
        print('Asynchronous operation completed (in then()).');
      }
    });
    super.dispose();
  }

  @override
  void initState() {
    super.initState();
    // This method is called when this State is created. You can use it to
    // initialize any data that this State needs (for example, the initial value
    // of _counter).
    //
    // If you want to do something when the widget is first built, you can do
    // that in the build method instead.
    _initHub();
    _loadFiles();

    File largeFile = File('/storage/emulated/0/Download/Pandora\'s Tower (Europe) (En,Fr,De,Es,It).iso');
    String uploadApiUrl = 'http://192.168.2.105:5001/api/uploadchunk/uploadchunk';

    uploadLargeFileHttp(largeFile, uploadApiUrl)
        .then((_) {
          if (kDebugMode) {
            print('File upload completed successfully!');
          }
        })
        .catchError((error) {
          if (kDebugMode) {
            print('Error during file upload: $error');
          }
        });
  }

  @override
  Widget build(BuildContext context) {
    // This method is rerun every time setState is called, for instance as done
    // by the _incrementCounter method above.
    //
    // The Flutter framework has been optimized to make rerunning build methods
    // fast, so that you can just rebuild anything that needs updating rather
    // than having to individually change instances of widgets.
    return Scaffold(
      appBar: AppBar(
        // TRY THIS: Try changing the color here to a specific color (to
        // Colors.amber, perhaps?) and trigger a hot reload to see the AppBar
        // change color while the other colors stay the same.
        backgroundColor: Theme.of(context).colorScheme.inversePrimary,
        // Here we take the value from the MyHomePage object that was created by
        // the App.build method, and use it to set our appbar title.
        title: Text(widget.title),
      ),
      body: ListView.builder(
        itemCount: _listViewAPIModel?.files?.length,
        itemBuilder: (context, index) {
          return ListTile(
            title: Text(_listViewAPIModel?.files?[index].path ?? ''),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () {
          // This method is called when the user presses the + button. You can
          // use it to add a new file or perform any other action.
          // For example, you can create a new file and add it to the list.
          // _createNewFile();
        },
        // This is the code that will be executed when the button is pressed.
        tooltip: 'Increment',
        child: const Icon(Icons.add),
      ), // This trailing comma makes auto-formatting nicer for build methods.
    );
  }
}
