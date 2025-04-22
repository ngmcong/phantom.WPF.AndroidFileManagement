class ListViewAPIModel {
  String? path;
  List<ListViewModel>? files;
  ListViewAPIModel({
    this.path,
    this.files,
  });
  Map<String, dynamic> toJson() {
    return {
      'path': path,
      'files': files?.map((file) => file.toJson()).toList(),
    };
  }
}

class ListViewModel {
  String? name;
  String? path;
  String? type;
  String? size;
  String? dateModified;

  ListViewModel({
    this.name,
    this.path,
    this.type,
    this.size,
    this.dateModified,
  });

  Map<String, dynamic> toJson() {
    return {
      'name': name,
      'path': path,
      'type': type,
      'size': size,
      'dateModified': dateModified,
    };
  }
}
